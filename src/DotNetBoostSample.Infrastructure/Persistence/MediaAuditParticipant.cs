using System.Globalization;
using System.Text.Json;
using DotNetBoost.Auditing.Core.Interfaces;
using DotNetBoost.Auditing.Core.Models;
using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.Media.Core.Models;
using DotNetBoost.Media.EntityFrameworkCore;

namespace DotNetBoostSample.Infrastructure.Persistence;

/// <summary>
/// Records what happens to media as entries people can read — "Product 42's gallery gained
/// desk.png", "Category 4's banner went from old.png to new.png" — in the same transaction as the
/// media rows.
///
/// <para><b>Why not the interceptor.</b> DotNetBoost.Media stores a file as a <c>MediaFile</c> row
/// and each use of it as a <c>MediaAttachment</c> row, and the interceptor sees rows. So attaching a
/// photo to a product wrote a <c>MediaAttachment</c> <c>Created</c> entry addressed by the link's
/// own GUID, and a soft delete wrote a <c>MediaFile</c> <c>Updated</c> entry whose only change was
/// <c>DeletedAt</c>. Product 42's own history said nothing at all. The media library tells an
/// <see cref="IMediaWriteParticipant"/> about each operation — one change per attach, detach,
/// replacement, reorder or delete — so both row types are ignored by the capture and this writes
/// the entries instead.</para>
///
/// <para><b>The format.</b> A change to which files an entity uses is an <c>Updated</c> entry on
/// that entity, with one row named after the role and the file names as its values, and the
/// operation in <c>Metadata</c>:</para>
/// <list type="table">
///   <item><term>Attach</term><description><c>Banner | — | photo.png</c>, <c>{"media":"Attach","fileId":…}</c></description></item>
///   <item><term>Detach</term><description><c>Banner | photo.png | —</c>, <c>{"media":"Detach","fileId":…}</c></description></item>
///   <item><term>Replace</term><description><c>Banner | photo.png | new.png</c>, <c>{"media":"Replace","fileId":…,"previousFileId":…}</c></description></item>
///   <item><term>Reorder</term><description><c>Gallery | a.png, b.png | b.png, a.png</c>, <c>{"media":"Reorder"}</c></description></item>
/// </list>
/// <para>The entity is the owner Media reports, filed under its own <c>(entityName, entityId)</c>,
/// so the change shows up in the product's history beside its price changes. That works because
/// Media's owner address and Auditing's entity address are the same thing, Entity Addressing v1
/// (<c>KeyFormatAgreementTests</c> pins it). The file's id goes in <c>Metadata</c> rather than the
/// row, which keeps the row readable and still tells two files with the same name apart.</para>
///
/// <para>An upload, an edit and a delete are about the <em>file</em>, and are filed under
/// <see cref="FileEntityName"/> and the file's id. A delete also writes a detach on every entity the
/// file was still attached to — Media lists them on the one delete change — marked
/// <c>"reason":"FileDeleted"</c>, so a product's history says why its image went.</para>
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
/// <c>IAuditManager.RecordAsync</c> tracks each entry on the context the media store is about to
/// save. They go out in that save or not at all, and anything this throws aborts the media write.
/// Each entry and its change rows are enlisted with <see cref="EfCoreMediaWriteScope"/>, so a write
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

        foreach (var write in Describe(change))
        {
            var entry = await audit.RecordAsync(write, ct);

            // Null when Auditing chose not to record it — recording switched off, or an edit that
            // changed nothing it keeps. Nothing to enlist.
            if (entry is null) continue;

            // The whole graph, so a write that does not commit takes all of it back.
            mediaWrite.Add(entry);
            foreach (var row in entry.Changes)
                mediaWrite.Add(row);
        }
    }

    private static IEnumerable<AuditWrite> Describe(MediaChange change)
    {
        switch (change.Kind)
        {
            case MediaChangeKind.Attached:
                yield return OnOwner(change, Owner(change), change.Role, null, FileName(change.File, change.MediaFileId),
                    new { media = "Attach", fileId = change.MediaFileId });
                break;

            case MediaChangeKind.Detached:
                yield return OnOwner(change, Owner(change), change.Role, FileName(change.File, change.MediaFileId), null,
                    new { media = "Detach", fileId = change.MediaFileId });
                break;

            case MediaChangeKind.Replaced:
                yield return OnOwner(change, Owner(change), change.Role,
                    Names(change.Displaced.Select(d => d.File)), FileName(change.File, change.MediaFileId),
                    change.Displaced.Count == 1
                        ? new { media = "Replace", fileId = change.MediaFileId, previousFileId = change.Displaced[0].File.Id }
                        : (object)new { media = "Replace", fileId = change.MediaFileId, previousFileIds = change.Displaced.Select(d => d.File.Id) });
                break;

            case MediaChangeKind.Reordered:
                yield return OnOwner(change, Owner(change), change.Role,
                    Names(change.PreviousOrder), Names(change.Order),
                    new { media = "Reorder" });
                break;

            case MediaChangeKind.Uploaded:
                yield return OnFile(change, AuditAction.Created, Values(change.File));
                break;

            case MediaChangeKind.Updated:
                yield return OnFile(change, AuditAction.Updated, Values(change.File, before: change.Previous));
                break;

            case MediaChangeKind.Deleted:
                // Every entity it was still attached to loses it, and says why.
                foreach (var link in change.Attachments)
                {
                    yield return OnOwner(change, link.Owner, link.Role, FileName(change.File, change.MediaFileId), null,
                        new { media = "Detach", fileId = change.MediaFileId, reason = "FileDeleted" });
                }

                yield return OnFile(change, AuditAction.Deleted, Values(change.File, deleted: true));
                break;

            default:
                throw new InvalidOperationException(
                    $"DotNetBoost.Media reported a {change.Kind} change, which this participant does not know how " +
                    "to record. Refusing the write rather than leaving it out of the trail.");
        }
    }

    /// <summary>A change to which files an entity uses, filed under that entity.</summary>
    private static AuditWrite OnOwner(
        MediaChange change, MediaOwner owner, string? role, string? oldValue, string? newValue, object metadata) => new()
    {
        EntityName    = owner.EntityName,
        EntityId      = owner.EntityId,
        Action        = AuditAction.Updated,
        Changes       = [new AuditChange(Label(role), oldValue, newValue)],
        CorrelationId = change.CorrelationId,
        Metadata      = JsonSerializer.Serialize(metadata),
    };

    /// <summary>A change to the file itself, filed under <see cref="FileEntityName"/>.</summary>
    private static AuditWrite OnFile(MediaChange change, AuditAction action, IReadOnlyList<AuditChange> changes) => new()
    {
        EntityName    = FileEntityName,
        EntityId      = (change.MediaFileId ?? throw new InvalidOperationException(
                            $"A {change.Kind} media change arrived without its file.")).ToString("D"),
        EntityType    = typeof(MediaFileInfo),
        Action        = action,
        Changes       = changes,
        CorrelationId = change.CorrelationId,
    };

    private static MediaOwner Owner(MediaChange change) =>
        change.Owner ?? throw new InvalidOperationException($"A {change.Kind} media change arrived without its owner.");

    /// <summary>
    /// A role as a row name: <c>banner</c> reads as <c>Banner</c>. Media stores roles lower-cased,
    /// so this is presentation only — the role itself is unchanged.
    /// </summary>
    private static string Label(string? role) =>
        string.IsNullOrEmpty(role)
            ? "Media"
            : char.ToUpperInvariant(role[0]) + role[1..];

    /// <summary>How a row names a file. Its id is in the entry's metadata.</summary>
    private static string FileName(MediaFileInfo? file, Guid? id) =>
        file?.FileName ?? id?.ToString("D") ?? "(unknown file)";

    /// <summary>Several files, in order, as one value; <c>null</c> for none.</summary>
    private static string? Names(IEnumerable<MediaFileInfo> files)
    {
        var names = string.Join(", ", files.Select(f => f.FileName));
        return names.Length == 0 ? null : names;
    }

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
            // An upload or a delete records what the file had; a property it never had — no tags,
            // no category — is not a row. An edit records every property that moved, including
            // one cleared to nothing.
            if (!edit && value is null)
                return;

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
}
