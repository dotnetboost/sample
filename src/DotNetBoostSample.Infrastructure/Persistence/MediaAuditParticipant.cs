using System.Globalization;
using DotNetBoost.Auditing.Core.Interfaces;
using DotNetBoost.Auditing.Core.Models;
using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.Media.Core.Models;
using DotNetBoost.Media.EntityFrameworkCore;

namespace DotNetBoostSample.Infrastructure.Persistence;

/// <summary>
/// Records what happens to media as entries people can read — "Product 42's gallery gained
/// desk.png", "desk.png was deleted" — in the same transaction as the media rows.
///
/// <para><b>Why not the interceptor.</b> DotNetBoost.Media stores a file as a <c>MediaFile</c> row
/// and each use of it as a <c>MediaAttachment</c> row, and the interceptor sees rows. So attaching a
/// photo to a product wrote a <c>MediaAttachment</c> <c>Created</c> entry addressed by the link's
/// own GUID, and a soft delete wrote a <c>MediaFile</c> <c>Updated</c> entry whose only change was
/// <c>DeletedAt</c>. Product 42's own history said nothing at all. The media library knows what
/// each write means — it calls <see cref="IMediaWriteParticipant"/> once per change, with the file,
/// the owner and the role — so both row types are ignored by the capture and this writes the
/// entries instead.</para>
///
/// <para><b>Where each change is filed.</b> An attach, a detach and a reorder are changes to the
/// <em>owner</em>: they are filed under the owner's <c>(entityName, entityId)</c>, as an
/// <c>Updated</c> entry with a <c>media.{role}</c> change, so they show up in the product's own
/// trail beside its price changes. That works because Media's owner address and Auditing's entity
/// address are the same thing, Entity Addressing v1 (<c>KeyFormatAgreementTests</c> pins it). An
/// upload, an edit and a delete are about the <em>file</em>, and are filed under
/// <see cref="FileEntityName"/> and the file's id. A delete is one entry under the file plus a detach
/// under each owner that still used it — all three with one correlation id.</para>
///
/// <para><b>Why the file entries are not called <c>MediaFile</c>.</b> <c>IgnoreEntity&lt;MediaFile&gt;()</c>
/// keeps the rows out, and Auditing honours an ignore by name when a write has no type — so an entry
/// addressed as <c>"MediaFile"</c> would be dropped. They are filed as <c>"Media"</c> with
/// <see cref="MediaFileInfo"/> as their type: the record Media describes a file with, which is what
/// the entry is about, and which is not ignored. A typed write also keeps Auditing from logging, on
/// every upload, that it recorded an undeclared entity with no model rules.</para>
///
/// <para><b>Why it commits with the change.</b> Exactly as for <see cref="SettingGroupAuditParticipant"/>:
/// this runs inside the media write, before the store saves; Media's EF Core store, Auditing's
/// EF Core store and this class all resolve the request's one <see cref="AppDbContext"/>, so
/// <c>IAuditManager.RecordAsync</c> tracks the entry on the context the media store is about to
/// save. It goes out in that save or not at all, and anything this throws aborts the media write.
/// The entry and each change row are enlisted with <see cref="EfCoreMediaWriteScope"/>, so a write
/// that does not commit takes all of them back off the context.</para>
///
/// <para>No actor is passed: Media has no notion of one, and a null actor falls back to the
/// request's caller, which is who made the change.</para>
/// </summary>
public sealed class MediaAuditParticipant(IAuditManager audit, EfCoreMediaWriteScope mediaWrite)
    : IMediaWriteParticipant
{
    /// <summary>The entity name a file's own entries are stored under.</summary>
    public const string FileEntityName = "Media";

    /// <inheritdoc/>
    public async Task OnWritingAsync(MediaChange change, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(change);

        var entry = await audit.RecordAsync(Describe(change), ct);

        // Null when Auditing chose not to record it — recording switched off, or an edit that
        // changed nothing it keeps. Nothing to enlist.
        if (entry is null) return;

        // The whole graph, so a write that does not commit takes all of it back.
        mediaWrite.Add(entry);
        foreach (var row in entry.Changes)
            mediaWrite.Add(row);
    }

    private static AuditWrite Describe(MediaChange change) => change.Kind switch
    {
        MediaChangeKind.Attached  => OnOwner(change, $"media.{change.Role}", null, Name(change)),
        MediaChangeKind.Detached  => OnOwner(change, $"media.{change.Role}", Name(change), null),
        MediaChangeKind.Reordered => OnOwner(change, $"media.{change.Role}.position",
                                         $"{Name(change)} #{change.PreviousSortOrder}",
                                         $"{Name(change)} #{change.SortOrder}"),

        MediaChangeKind.Uploaded  => OnFile(change, AuditAction.Created, Values(change.File)),
        MediaChangeKind.Updated   => OnFile(change, AuditAction.Updated, Values(change.File, before: change.Previous)),
        MediaChangeKind.Deleted   => OnFile(change, AuditAction.Deleted, Values(change.File, deleted: true)),

        _ => throw new InvalidOperationException(
            $"DotNetBoost.Media reported a {change.Kind} change, which this participant does not know how " +
            "to record. Refusing the write rather than leaving it out of the trail.")
    };

    /// <summary>A change to which files an entity uses, filed under that entity.</summary>
    private static AuditWrite OnOwner(MediaChange change, string property, string? oldValue, string? newValue)
    {
        var owner = change.Owner
            ?? throw new InvalidOperationException($"A {change.Kind} media change arrived without its owner.");

        return new AuditWrite
        {
            EntityName    = owner.EntityName,
            EntityId      = owner.EntityId,
            Action        = AuditAction.Updated,
            Changes       = [new AuditChange(property, oldValue, newValue)],
            CorrelationId = change.CorrelationId,
        };
    }

    /// <summary>A change to the file itself, filed under <see cref="FileEntityName"/>.</summary>
    private static AuditWrite OnFile(MediaChange change, AuditAction action, IReadOnlyList<AuditChange> changes) => new()
    {
        EntityName    = FileEntityName,
        EntityId      = change.MediaFileId.ToString("D"),
        EntityType    = typeof(MediaFileInfo),
        Action        = action,
        Changes       = changes,
        CorrelationId = change.CorrelationId,
    };

    /// <summary>
    /// The file's recorded properties as changes. An upload reports each as a new value and a
    /// delete each as an old one — the file as it was, which after the delete is the only copy of
    /// its name. An edit reports only what moved, from <paramref name="before"/> to
    /// <paramref name="file"/>.
    /// </summary>
    private static List<AuditChange> Values(MediaFileInfo? file, MediaFileInfo? before = null, bool deleted = false)
    {
        if (file is null) return [];

        var edit    = before is not null;
        var changes = new List<AuditChange>();

        void Add(string name, string? value, string? previous = null)
        {
            if (deleted)
                changes.Add(new AuditChange(name, value, null));
            else if (!edit || !string.Equals(value, previous, StringComparison.Ordinal))
                changes.Add(new AuditChange(name, previous, value));
        }

        Add(nameof(MediaFileInfo.FileName), file.FileName, before?.FileName);
        Add(nameof(MediaFileInfo.Category), file.Category, before?.Category);
        Add(nameof(MediaFileInfo.Tags), Join(file.Tags), before is null ? null : Join(before.Tags));
        Add(nameof(MediaFileInfo.Metadata), Join(file.Metadata), before is null ? null : Join(before.Metadata));

        // Fixed at upload, so never part of an edit.
        if (!edit)
        {
            Add(nameof(MediaFileInfo.ContentType), file.ContentType);
            Add(nameof(MediaFileInfo.Size), file.Size.ToString(CultureInfo.InvariantCulture));
        }

        return changes;
    }

    private static string? Join(IReadOnlyList<string> tags) => tags.Count == 0 ? null : string.Join(",", tags);

    private static string? Join(IReadOnlyDictionary<string, string> metadata) =>
        metadata.Count == 0
            ? null
            : string.Join(";", metadata.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));

    /// <summary>How an entry names a file: its name and its id, since names repeat.</summary>
    private static string Name(MediaChange change) =>
        change.File is { } file
            ? $"{file.FileName} ({change.MediaFileId:D})"
            : change.MediaFileId.ToString("D");
}
