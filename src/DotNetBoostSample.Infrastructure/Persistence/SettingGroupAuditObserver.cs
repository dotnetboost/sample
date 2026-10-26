using DotNetBoost.Auditing.Core.Interfaces;
using DotNetBoost.Auditing.Core.Models;
using DotNetBoost.Settings.Core.Attributes;
using DotNetBoost.Settings.Core.Interfaces;
using DotNetBoostSample.Application.Settings;
using System.Reflection;

namespace DotNetBoostSample.Infrastructure.Persistence;

/// <summary>
/// Records one audit entry per save of a settings group, rather than one per row of the
/// Settings table.
///
/// <para><b>Why not the interceptor.</b> DotNetBoost.Settings stores a group as one row per
/// property, so the interceptor — which sees rows — wrote a separate <c>Setting</c> entry for every
/// value that moved, addressed by a row id nobody reading the trail knows. The settings library
/// already knows where an edit begins and ends: <see cref="ISettingWriteObserver"/> is called once
/// per save, with the whole group's diff. So the <c>Setting</c> rows are ignored by the capture,
/// and this writes the entry instead, still under <see cref="EntityName"/> but addressed by the
/// group's name.</para>
///
/// <para><b>Why the entry carries the group's type.</b> <c>IgnoreEntity&lt;Setting&gt;()</c> also
/// suppresses a write addressed by the name <c>"Setting"</c> alone, so an entry with no type would
/// be dropped as ignored. Auditing checks the ignore by type when the write has one, and the type
/// this entry is about is the group's class — <see cref="CatalogSettings"/>, say — which is not
/// ignored. It is also where the model's rules come from: an <c>[AuditIgnore]</c> or
/// <c>[AuditRedact]</c> on a group's property is honoured, exactly as on an entity.</para>
///
/// <para><b>What it costs.</b> The interceptor's guarantee — a change and its entry in one
/// <c>SaveChanges</c> — does not hold here. The observer runs after the settings write has
/// committed, and DotNetBoost.Settings logs and swallows anything it throws, so a failure to record
/// leaves the change standing without an entry. For configuration edited through a dashboard that
/// is the right trade for a readable trail; for anything stricter, it is not.</para>
///
/// <para><b>Secrets.</b> DotNetBoost.Settings has already withheld the values of every
/// <c>[Sensitive]</c> property, and this marks those changes redacted on the entry, so the trail
/// says "changed, value withheld" rather than "changed from nothing to nothing".</para>
/// </summary>
public sealed class SettingGroupAuditObserver(IAuditManager audit, AppDbContext db) : ISettingWriteObserver
{
    /// <summary>The entity name a settings group's entries are stored under.</summary>
    public const string EntityName = "Setting";

    /// <summary>
    /// Every settings group in the application assembly, by the name its rows are stored under —
    /// the same assembly <c>UseFluentValidation</c> scans, resolved the way DotNetBoost.Settings
    /// resolves it.
    /// </summary>
    private static readonly Dictionary<string, Type> Groups = typeof(BrandingSettings).Assembly
        .GetTypes()
        .Where(type => type.GetCustomAttribute<SettingGroupAttribute>() is not null)
        .ToDictionary(SettingGroupAttribute.ResolveName, StringComparer.Ordinal);

    /// <inheritdoc/>
    public async Task OnWrittenAsync(SettingWrite write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);

        // Thrown rather than recorded without a type, which would be dropped as ignored and say
        // nothing. DotNetBoost.Settings logs it with the group's name.
        if (!Groups.TryGetValue(write.GroupName, out var groupType))
            throw new InvalidOperationException(
                $"Settings group '{write.GroupName}' has no [SettingGroup] class in "
                + $"{typeof(BrandingSettings).Assembly.GetName().Name}, so its save cannot be audited.");

        var entry = await audit.RecordAsync(new AuditWrite
        {
            EntityName    = EntityName,
            EntityId      = write.GroupName,
            EntityType    = groupType,
            Action        = write.Kind == SettingWriteKind.Cleared ? AuditAction.Deleted : AuditAction.Updated,
            Changes       = [.. write.Changes.Select(c => new AuditChange(c.PropertyName, c.OldValue, c.NewValue))],
            // The settings library's id for the save, so the entry joins the request it came from.
            CorrelationId = write.CorrelationId,
        }, cancellationToken);

        if (entry is null) return;

        var redacted = write.Changes.Where(c => c.IsRedacted).Select(c => c.PropertyName).ToHashSet();
        foreach (var change in entry.Changes.Where(c => redacted.Contains(c.PropertyName)))
            change.IsRedacted = true;

        // The EF Core store tracks the entry rather than saving it, and the save it would normally
        // ride along with has already happened — so this one is the entry's own.
        await db.SaveChangesAsync(cancellationToken);
    }
}
