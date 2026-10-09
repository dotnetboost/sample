using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Metadata;

namespace DotNetBoostSample.Application.Composition;

/// <summary>
/// Per-language alt text and captions for an uploaded file — DotNetBoost.Media and
/// DotNetBoost.MultiLingual composed, with neither library referencing the other and no
/// integration package between them.
///
/// <para><b>How it works.</b> Both libraries address a subject by the same two strings: an entity
/// name and an entity id, under the rules of DotNetBoost Entity Addressing v1. MultiLingual files
/// translations against <c>(EntityName, EntityId, PropertyName, LanguageCode)</c>; Media files
/// attachments against <c>(OwnerEntityName, OwnerEntityId)</c>. So this class declares one subject
/// name — <see cref="Subject"/> — and uses a media file's own <see cref="Guid"/> as the entity id.
/// The translation rows and the media row are about the same thing because this application says
/// they are. Neither library has an opinion, and no new table is needed.</para>
///
/// <para><b>What this used to be.</b> Four times this length. MultiLingual could only address an
/// entity it owned the type of, so this class dropped to the raw <c>ITranslationStore</c> and
/// reimplemented four things the library already knew: how to format a key, how to fall back from
/// the current language to the default, how to read many subjects in one query, and how to build a
/// batch write. The fallback copy was the dangerous one — the library's own rule, reimplemented
/// here, free to drift from the interceptors that translate every entity. <c>For(name, id)</c> and
/// <c>ForMany(name, ids)</c> arrived in preview.2 and all of it went.</para>
///
/// <para><b>And what it no longer does.</b> Writing and reading every language used to be here too,
/// behind this application's own <c>/api/media-text</c> endpoints, because
/// DotNetBoost.MultiLingual.API shipped none. It ships them now, so the dashboard's alt-text editor
/// talks to <c>/api/translations/MediaFile/{id}</c> directly and the write side of this class went
/// with the endpoints.</para>
///
/// <para>What is left is the read this application needs in its OWN responses — a product listing
/// carries its images' alt text, which no media endpoint can know about — plus the cleanup on
/// delete, and the subject name that ties the two stores together. That is the whole integration
/// surface.</para>
/// </summary>
public sealed class MediaText(IMultiLingualManager multiLingual)
{
    /// <summary>
    /// The entity name these translation rows are filed under. Media resolves a <c>MediaFile</c>'s
    /// own type name to the same string, so the two stores line up when read side by side — and
    /// because it is a <c>const</c>, renaming it is one edit rather than a search for a scattered
    /// literal. tests/KeyFormatAgreementTests.cs asserts it is the name Media resolves.
    /// </summary>
    public const string Subject = "MediaFile";

    /// <summary>Alt text — required for accessibility, and different in every language.</summary>
    public const string AltText = "AltText";

    /// <summary>A longer visible caption.</summary>
    public const string Caption = "Caption";

    /// <summary>
    /// A media file's id as the string the rows are stored under — formatted by MultiLingual's own
    /// <see cref="EntityKey"/> rather than by hand here, so this application is not a third
    /// implementation of a rule two libraries already share.
    /// </summary>
    private static string Key(Guid mediaFileId) => EntityKey.ToKeyString(mediaFileId);

    /// <summary>
    /// Alt text and caption for one file in the language this request resolved to, falling back to
    /// the default language for anything missing — the library's own fallback rule, the same one
    /// its EF Core interceptors apply to a translated entity. A file and the list it sits in
    /// therefore cannot resolve its text differently.
    /// </summary>
    public async Task<MediaTextValues> GetAsync(Guid mediaFileId, CancellationToken ct = default) =>
        Values(await multiLingual.For(Subject, mediaFileId).GetAsync(ct));

    /// <summary>
    /// Alt text and captions for many files at once, in the current language — one query, not one
    /// per file. Every id asked about is present in the result, so a caller reading a file nobody
    /// wrote text for gets empty values rather than a missing key.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, MediaTextValues>> GetManyAsync(
        IReadOnlyCollection<Guid> mediaFileIds,
        CancellationToken ct = default)
    {
        // Distinct, because the caller may legitimately pass the same file twice: Media lets one
        // file be attached under several roles at once, so a product's primary image can also sit
        // in its gallery. Keying a dictionary on a repeated id would throw.
        var ids = mediaFileIds.Distinct().ToArray();

        if (ids.Length == 0) return new Dictionary<Guid, MediaTextValues>();

        var byId = await multiLingual.ForMany(Subject, [.. ids.Cast<object>()]).GetAsync(ct);

        return ids.ToDictionary(id => id, id => Values(byId.GetValueOrDefault(Key(id))));
    }

    /// <summary>
    /// Removes every translation row for a batch of files, in one statement, and answers how many
    /// went.
    ///
    /// <para>The gap this fills: deleting a <c>MediaFile</c> does not delete its translations,
    /// because the media library has no idea they exist. <see cref="MediaTextCleanup"/> is
    /// registered as an <c>IMediaFilesDeletedHandler</c> and calls this for every delete, so no
    /// call site has to remember it.</para>
    /// </summary>
    public Task<int> DeleteManyAsync(IReadOnlyCollection<Guid> mediaFileIds, CancellationToken ct = default) =>
        mediaFileIds.Count == 0
            ? Task.FromResult(0)
            : multiLingual.ForMany(Subject, [.. mediaFileIds.Distinct().Cast<object>()]).DeleteAsync(ct);

    /// <summary>The two properties this application cares about, out of whatever is stored.</summary>
    private static MediaTextValues Values(IReadOnlyDictionary<string, string?>? values) =>
        new(values?.GetValueOrDefault(AltText), values?.GetValueOrDefault(Caption));
}

/// <summary>Alt text and caption resolved for one file in one language.</summary>
public sealed record MediaTextValues(string? AltText, string? Caption);
