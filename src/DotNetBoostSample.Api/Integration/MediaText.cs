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
    /// Writes the alt text and caption of one file in every language supplied — one call, one
    /// batch. A null value removes the stored row rather than storing a null, which is the store's
    /// own contract and is unchanged by batching.
    ///
    /// <para><b>Why not one call per property per language.</b> This replaced a
    /// <c>SetAsync(file, language, property, value)</c> that the endpoint called in a nested loop:
    /// two awaits a language, so six round trips to save three languages of a single file, each one
    /// wrapping a single row in an array for the batch API it was already calling.</para>
    ///
    /// <para>The cost that mattered was not the round trips though — it was that each was its own
    /// write. A failure part-way left some languages saved and the rest not, with nothing in the
    /// response to say where it stopped, on a screen whose whole purpose is editing several
    /// languages at once. <c>UpsertManyAsync</c> takes the lot and is atomic where the backend
    /// supports it, so the edit lands whole or not at all.</para>
    /// </summary>
    public Task SetManyAsync(
        Guid mediaFileId,
        IReadOnlyDictionary<string, MediaTextValues> byLanguage,
        CancellationToken ct = default)
    {
        var entityId = Key(mediaFileId);

        var rows = byLanguage
            .SelectMany(language => new[]
            {
                Row(entityId, language.Key, AltText, language.Value.AltText),
                Row(entityId, language.Key, Caption, language.Value.Caption)
            })
            .ToArray();

        // No languages supplied, so no statement to send — the same answer GetManyAsync gives an
        // empty id list, rather than asking the store to write nothing.
        return rows.Length == 0 ? Task.CompletedTask : translations.UpsertManyAsync(rows, ct);
    }

    /// <summary>One row of the batch, so the four parts of a translation's key are named once.</summary>
    private static Translation Row(
        string entityId, string languageCode, string propertyName, string? value) => new()
    {
        EntityName   = Subject,
        EntityId     = entityId,
        PropertyName = propertyName,
        LanguageCode = languageCode,
        Value        = value
    };

    /// <summary>
    /// Alt text and caption for one file in the language this request resolved to, falling back
    /// to the default language for anything missing.
    /// <para>
    /// One id through <see cref="GetManyAsync"/> rather than a read of its own: the fallback rule is
    /// the thing here most worth having exactly one copy of, because a single file resolving it
    /// differently from the list it sits in shows two alt texts for one image.
    /// <see cref="GetManyAsync"/> answers for every id it is given, so the lookup cannot miss.
    /// </para>
    /// </summary>
    public async Task<MediaTextValues> GetAsync(Guid mediaFileId, CancellationToken ct = default) =>
        (await GetManyAsync([mediaFileId], ct))[mediaFileId];

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

        // Collapsed to one code when they are the same, so the query does not ask for a language
        // twice. This is the only place the fallback pair is resolved — GetAsync comes through here.
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
