using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Metadata;
using Microsoft.EntityFrameworkCore;

namespace DotNetBoostSample.Application.Catalog.Shared;

/// <summary>
/// Reading one entity with <b>every</b> language's text, for the two detail slices an editor loads:
/// <c>GET /api/categories/{id}</c> and <c>GET /api/products/{id}</c>.
///
/// <para>Everywhere else in this application the interceptors have already done the work — a plain
/// <c>db.Products.ToListAsync()</c> comes back in the request's language. An editor is the exception:
/// it draws a box per language, so it needs all of them at once, which is
/// <c>For(entity).GetAllAsync()</c>.</para>
///
/// <para><b>Why that is not the whole answer with a base language configured.</b>
/// <c>GetAllAsync()</c> returns stored TRANSLATIONS, and in base-language mode the base language is
/// not one — it is the entity's own columns. Left at that, the editor would open with the base
/// language's boxes empty and save that emptiness over the canonical text on the first write. So
/// this reads the entity in the base language, takes those values off the entity, and puts them in
/// the map under the base language's code: the same shape, the same keys, one more language in it,
/// and a response a client cannot tell the two modes apart from.</para>
///
/// <para>The write half of the same decision is <see cref="TranslationWrites"/>. These two files
/// are the only ones in the solution that ask which mode is on.</para>
/// </summary>
public static class TranslationReads
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> NoText =
        new Dictionary<string, IReadOnlyDictionary<string, string?>>();

    /// <summary>
    /// The single entity <paramref name="one"/> selects, and every language of text it has.
    ///
    /// <para>Takes the query rather than the loaded entity because, in base-language mode, WHICH
    /// language it is read in is part of the answer: the translatable columns of a tracked entity
    /// hold whatever language the query resolved, so an entity read under <c>Accept-Language: ar</c>
    /// has the Arabic name in the property where the base value lives in the database. Reading it in
    /// the base language is what makes <c>entity.Name</c> the base value again.</para>
    ///
    /// <para>The request's own language is restored before this returns, and nothing in either
    /// detail response is affected by the detour: neither carries a localized name — the text all
    /// travels in the map — and the alt text beside their images, which <em>is</em> resolved in the
    /// request's language, is read by the slice afterwards.</para>
    ///
    /// <para>Outside base-language mode this is the two lines it replaces: the query, then
    /// <c>GetAllAsync()</c>. No language is changed and nothing is merged.</para>
    /// </summary>
    /// <returns>
    /// The entity, or <c>null</c> when the query matched nothing — in which case the text is empty
    /// and the slice answers 404.
    /// </returns>
    public static async Task<(T? Entity, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> Text)>
        LoadAsync<T>(IMultiLingualManager multiLingual, IQueryable<T> one, CancellationToken ct)
        where T : class
    {
        var baseLanguage = multiLingual.Languages.BaseLanguage;

        if (baseLanguage is null)
        {
            var found = await one.FirstOrDefaultAsync(ct);

            return found is null
                ? (null, NoText)
                : (found, await multiLingual.For(found).GetAllAsync(ct));
        }

        var requested = multiLingual.IsLanguageResolved ? multiLingual.CurrentLanguage.Code : null;
        var detour = requested is not null && !multiLingual.Languages.IsBaseLanguage(requested);

        if (detour) await multiLingual.UseLanguageAsync(baseLanguage, ct);

        T? entity;

        try
        {
            entity = await one.FirstOrDefaultAsync(ct);
        }
        finally
        {
            if (detour) await multiLingual.UseLanguageAsync(requested!, ct);
        }

        if (entity is null) return (null, NoText);

        // The base language's values, read off the entity while they are still the base language's.
        // Captured after the restore above and not before it: the properties were filled by the
        // query, and putting the scope's language back does not touch an entity already in memory.
        var baseValues = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var property in MultiLingualMetadata.For(typeof(T)))
            baseValues[property.Name] = property.GetValue(entity);

        // Case-insensitive, because the two halves name the language independently: the keys come
        // from the translation rows and this one from the configured option. Both are canonical
        // today, and a map with "en" and "EN" in it would be a shape no client should have to read.
        var text = new Dictionary<string, IReadOnlyDictionary<string, string?>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var (language, values) in await multiLingual.For(entity).GetAllAsync(ct))
            text[language] = values;

        text[baseLanguage] = baseValues;

        return (entity, text);
    }
}
