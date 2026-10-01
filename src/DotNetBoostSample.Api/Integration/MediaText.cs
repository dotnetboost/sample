using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Models;

namespace DotNetBoostSample.Api.Integration;

/// <summary>
/// Per-language alt text and captions for an uploaded file — DotNetBoost.Media and
/// DotNetBoost.MultiLingual composed, with neither library referencing the other and no
/// integration package between them.
///
/// <para><b>How it works.</b> Both libraries address a subject by the same two strings: an entity
/// name and an entity id. MultiLingual stores translations against
/// <c>(EntityName, EntityId, PropertyName, LanguageCode)</c>; Media stores attachments against
/// <c>(OwnerEntityName, OwnerEntityId)</c> and exposes
/// <c>IMediaManager.For(string entityName, string entityId)</c> for callers that hold the pair
/// rather than an object. That shared addressing convention is the whole integration surface.</para>
///
/// <para>So this class declares one subject name — <see cref="Subject"/> — and uses a media
/// file's own <see cref="Guid"/> as the entity id. The translation rows and the media row are
/// about the same thing because this application says they are. Neither library has an opinion,
/// and no new table is needed: alt text lives in the translations table that MultiLingual already
/// owns.</para>
///
/// <para><b>The one rule.</b> The id must be formatted the way both libraries format it. Both use
/// <c>Guid.ToString("D")</c>, invariantly. <see cref="Key"/> is the single place this file relies
/// on that, which is why it exists as its own method rather than being inlined — and why
/// tests/DotNetBoostSample.Tests asserts the agreement rather than trusting it.</para>
/// </summary>
public sealed class MediaText(ITranslationStore translations, IMultiLingualManager multiLingual)
{
    /// <summary>
    /// The entity name these translation rows are filed under. Media resolves a
    /// <c>MediaFile</c>'s own type name to the same string, so the two stores line up when read
    /// side by side — and because it is a <c>const</c>, renaming it is one edit, not a search for
    /// a scattered literal.
    /// </summary>
    public const string Subject = "MediaFile";

    /// <summary>Alt text — required for accessibility, and different in every language.</summary>
    public const string AltText = "AltText";

    /// <summary>A longer visible caption.</summary>
    public const string Caption = "Caption";

    /// <summary>
    /// A media file's id as the string both libraries agree on: invariant, "D" format.
    /// </summary>
    private static string Key(Guid mediaFileId) => mediaFileId.ToString("D");

    /// <summary>
    /// Writes one property of one file in one language. A null <paramref name="value"/> removes
    /// the stored row rather than storing a null, which is the store's own contract.
    /// </summary>
    public Task SetAsync(
        Guid mediaFileId,
        string languageCode,
        string propertyName,
        string? value,
        CancellationToken ct = default) =>
        translations.UpsertManyAsync(
            [
                new Translation
                {
                    EntityName   = Subject,
                    EntityId     = Key(mediaFileId),
                    PropertyName = propertyName,
                    LanguageCode = languageCode,
                    Value        = value
                }
            ],
            ct);

    /// <summary>
    /// Alt text and caption for one file in the language this request resolved to, falling back
    /// to the default language for anything missing.
    /// </summary>
    public async Task<MediaTextValues> GetAsync(Guid mediaFileId, CancellationToken ct = default)
    {
        var current = multiLingual.CurrentLanguage.Code;
        var fallback = multiLingual.DefaultLanguage.Code;

        var languages = current == fallback ? new[] { current } : [current, fallback];

        var rows = await translations.GetAsync(Subject, [Key(mediaFileId)], languages, ct);

        return new MediaTextValues(
            Pick(rows, AltText, current, fallback),
            Pick(rows, Caption, current, fallback));
    }

    /// <summary>
    /// Alt text and captions for many files at once, in the current language — one query, not one
    /// per file. The reason <see cref="ITranslationStore"/> is used directly here rather than the
    /// friendlier <c>multiLingual.For(entity)</c>: there is no entity instance to pass, only ids.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, MediaTextValues>> GetManyAsync(
        IReadOnlyCollection<Guid> mediaFileIds,
        CancellationToken ct = default)
    {
        // Distinct, because the caller may legitimately pass the same file twice: Media lets one
        // file be attached under several roles at once, so a product's primary image can also sit
        // in its gallery. Keying a dictionary on a repeated id would throw.
        var ids = mediaFileIds.Distinct().ToArray();

        if (ids.Length == 0)
            return new Dictionary<Guid, MediaTextValues>();

        var current  = multiLingual.CurrentLanguage.Code;
        var fallback = multiLingual.DefaultLanguage.Code;

        var languages = current == fallback ? new[] { current } : [current, fallback];

        var rows = await translations.GetAsync(
            Subject, [.. ids.Select(Key)], languages, ct);

        var byEntity = rows.GroupBy(r => r.EntityId).ToDictionary(g => g.Key, g => g.ToList());

        return ids.ToDictionary(
            id => id,
            id =>
            {
                var forFile = byEntity.GetValueOrDefault(Key(id)) ?? [];
                return new MediaTextValues(
                    Pick(forFile, AltText, current, fallback),
                    Pick(forFile, Caption, current, fallback));
            });
    }

    /// <summary>
    /// Every language's values for one file — what an editor screen needs to show all languages
    /// side by side.
    /// </summary>
    public async Task<IReadOnlyList<Translation>> GetAllLanguagesAsync(
        Guid mediaFileId,
        CancellationToken ct = default) =>
        await translations.GetAllAsync(Subject, Key(mediaFileId), ct);

    /// <summary>
    /// Removes every translation row for a file. The gap this fills: deleting a
    /// <see cref="DotNetBoost.Media.Core.Models.MediaFile"/> does not delete its translations,
    /// because the media library has no idea they exist. Call this alongside the delete — see
    /// Endpoints/MediaTextEndpoints.cs.
    /// </summary>
    public Task DeleteAsync(Guid mediaFileId, CancellationToken ct = default) =>
        translations.DeleteEntitiesAsync(Subject, [Key(mediaFileId)], ct);

    private static string? Pick(
        IEnumerable<Translation> rows,
        string propertyName,
        string current,
        string fallback)
    {
        var candidates = rows.Where(r => r.PropertyName == propertyName).ToList();

        return candidates.FirstOrDefault(r => r.LanguageCode == current)?.Value
               ?? candidates.FirstOrDefault(r => r.LanguageCode == fallback)?.Value;
    }
}

/// <summary>Alt text and caption resolved for one file in one language.</summary>
public sealed record MediaTextValues(string? AltText, string? Caption);
