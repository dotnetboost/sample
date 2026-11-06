using DotNetBoost.Auditing.Core.Interfaces;
using DotNetBoost.Auditing.Core.Models;
using DotNetBoost.Settings.Core.Interfaces;
using DotNetBoost.Settings.EntityFrameworkCore;

namespace DotNetBoostSample.Infrastructure.Persistence;

/// <summary>
/// Records one audit entry per save of a settings group, rather than one per row of the
/// Settings table — in the same transaction as the save.
///
/// <para><b>Why not the interceptor.</b> DotNetBoost.Settings stores a group as one row per
/// property, so the interceptor — which sees rows — wrote a separate <c>Setting</c> entry for every
/// value that moved, addressed by a row id nobody reading the trail knows. The settings library
/// already knows where an edit begins and ends: <see cref="ISettingWriteParticipant"/> is called
/// once per write, with the whole group's diff. So the <c>Setting</c> rows are ignored by the
/// capture, and this writes the entry instead, still under <see cref="EntityName"/> but addressed by
/// the group's name.</para>
///
/// <para><b>Why it commits with the change.</b> A participant runs inside the settings write,
/// before the store saves. In the request's scope, Settings' EF Core store, Auditing's EF Core store
/// and this class all resolve the same scoped <see cref="AppDbContext"/>, so
/// <c>IAuditManager.RecordAsync</c> tracks the entry on the very context the settings store is
/// about to save. The entry goes out in that save, or not at all — and anything this throws aborts
/// the settings write, so a failure to record fails the request rather than passing silently.
/// Nothing here saves, and nothing here reaches outside the context: the write can still fail
/// after this has run.</para>
///
/// <para><b>Why the change rows are enlisted too.</b> If the write does not commit,
/// <see cref="EfCoreSettingWriteScope"/> detaches only the entities passed to <c>Add</c>, not ones
/// reached through navigations. Auditing tracks an entry together with its change rows, so each is
/// enlisted, or a failed write would leave them <c>Added</c> on the request's context for its next
/// <c>SaveChangesAsync</c> to insert.</para>
///
/// <para><b>Why the entry carries the group's type.</b> <c>IgnoreEntity&lt;Setting&gt;()</c> also
/// suppresses a write addressed by the name <c>"Setting"</c> alone, so an entry with no type would
/// be dropped as ignored. Auditing checks the ignore by type when the write has one, and the type
/// this entry is about is the group's class — <c>CatalogSettings</c>, say — which is not ignored.
/// It is also where the model's rules come from: an <c>[AuditIgnore]</c> or <c>[AuditRedact]</c>
/// on a group's property is honoured, exactly as on an entity. The settings library hands that type
/// over as <see cref="SettingWrite.GroupType"/>.</para>
///
/// <para><b>Secrets.</b> DotNetBoost.Settings has already withheld the values of every
/// <c>[Sensitive]</c> property, and this marks those changes redacted on the entry, so the trail
/// says "changed, value withheld" rather than "changed from nothing to nothing".</para>
/// </summary>
public sealed class SettingGroupAuditParticipant(IAuditManager audit, EfCoreSettingWriteScope settingsWrite)
    : ISettingWriteParticipant
{
    /// <summary>The entity name a settings group's entries are stored under.</summary>
    public const string EntityName = "Setting";

    /// <inheritdoc/>
    public async Task OnWritingAsync(SettingWrite write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);

        // Always set by DotNetBoost.Settings; null only on a SettingWrite built by hand. Thrown
        // rather than recorded without a type, which would be dropped as ignored and say nothing.
        var groupType = write.GroupType
            ?? throw new InvalidOperationException(
                $"Settings group '{write.GroupName}' arrived without its type, so its save cannot be audited.");

        // Tracks the entry and its change rows on the request's AppDbContext — the one the settings
        // store is about to save. Nothing is saved here.
        var entry = await audit.RecordAsync(new AuditWrite
        {
            EntityName    = EntityName,
            EntityId      = write.GroupName,
            EntityType    = groupType,
            Action        = write.Kind == SettingWriteKind.Cleared ? AuditAction.Deleted : AuditAction.Updated,
            Changes       = [.. write.Changes.Select(c => new AuditChange(c.PropertyName, c.OldValue, c.NewValue))],
            // Who and which edit, from the write itself. A null actor still falls back to the
            // request's caller.
            Actor         = write.Actor,
            CorrelationId = write.CorrelationId,
        }, cancellationToken);

        if (entry is null) return;

        var redacted = write.Changes.Where(c => c.IsRedacted).Select(c => c.PropertyName).ToHashSet();
        foreach (var change in entry.Changes.Where(c => redacted.Contains(c.PropertyName)))
            change.IsRedacted = true;

        // The whole graph, so a write that does not commit takes all of it back.
        settingsWrite.Add(entry);
        foreach (var change in entry.Changes)
            settingsWrite.Add(change);
    }
}
