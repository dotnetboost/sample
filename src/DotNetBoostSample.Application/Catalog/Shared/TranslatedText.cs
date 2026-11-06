using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Metadata;
using DotNetBoostSample.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotNetBoostSample.Application.Catalog.Shared;

/// <summary>
/// An entity's text in every language: the read the two detail slices answer with, and the write
/// the four create and update slices make. <c>MediaText</c> for a <c>MediaFile</c>, this for a
/// <c>Category</c> or a <c>Product</c>.
///
/// <para><b>Why the read and the write are one class.</b> They are one decision, asked twice:
/// <em>where does a language live?</em> Without <c>MultiLingualOptions.BaseLanguage</c> the answer
/// is "a translation row, always". With one configured, one language is the entity's own columns
/// instead. Since DotNetBoost.MultiLingual 1.0.0-preview.3 the library answers both halves itself —
/// <c>SetAll</c> writes every language wherever it lives, and <c>GetAllAsync(includeBase: true)</c>
/// reads every language back the same way — so neither half has to change the request's language
/// any more. What is left here is the one rule a create needs (see <see cref="CreateAsync"/>).</para>
///
/// <para><b>It is also the whole of what this application knows about the mode.</b> Four handlers
/// hand it the <c>translations</c> map the request arrived with, two hand it a query, and none of
/// them asks which mode is on — which is what keeps the slices identical in both.</para>
///
/// <para><b>And one word of the name is deliberately loose.</b> In base-language mode the base
/// language's values are not a translation at all — they are the entity's own columns. "Translated
/// text" is what this application calls a property whose value depends on the language, wherever
/// the library keeps it.</para>
///
/// <para>Scoped, like <c>MediaText</c>: it holds the request's engine and nothing else.</para>
/// </summary>
public sealed class TranslatedText(IMultiLingualManager multiLingual)
{
    /// <summary>What a query that matched nothing has to say. Shared, because it never changes.</summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> None =
        new Dictionary<string, IReadOnlyDictionary<string, string?>>();

    /// <summary>
    /// The single entity <paramref name="one"/> selects, and every language of text it has, as
    /// languageCode → (propertyName → value) — the base language included, read from its columns.
    ///
    /// <para>This is the exception to "the interceptors already translated it". Everywhere else a
    /// plain <c>ToListAsync()</c> comes back in the request's language and that is the whole story;
    /// an editor draws a box per language, so it needs all of them at once.</para>
    ///
    /// <para>The entity is read in the request's own language. That puts the request's language in
    /// its translatable properties, which does not matter: neither detail response carries a
    /// localized name, and <c>GetAllAsync(includeBase: true)</c> reads the base language's columns
    /// from the database by key rather than off the properties.</para>
    /// </summary>
    /// <returns>The entity, or <c>null</c> when the query matched nothing — then the slice answers 404.</returns>
    public async Task<(T? Entity, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> Text)>
        LoadAsync<T>(IQueryable<T> one, CancellationToken ct)
        where T : class
    {
        var entity = await one.FirstOrDefaultAsync(ct);

        return entity is null
            ? (null, None)
            : (entity, await multiLingual.For(entity).GetAllAsync(includeBase: true, ct));
    }

    /// <summary>
    /// Writes a change to <paramref name="entity"/> and the languages in
    /// <paramref name="translations"/> in one <c>SaveChanges</c> — the row and its text in the same
    /// transaction, which is why <c>Category</c> and <c>Product</c> are deliberately absent from
    /// <c>ExposeOverHttp</c>: a second way in would be a second answer.
    ///
    /// <para>An update: only the languages and properties the request sent are written. Every one
    /// goes through <c>SetAll</c>, which puts the base language in the entity's columns and every
    /// other language in a translation row, whatever language the request is in. A property name the
    /// entity does not declare translatable is refused before anything is queued, in both modes
    /// alike, so a caller cannot tell from a refusal which mode the server is in.</para>
    /// </summary>
    /// <param name="db">The unit of work the row and its text are written in.</param>
    /// <param name="entity">A tracked entity carrying <c>[MultiLingual]</c> properties.</param>
    /// <param name="translations">languageCode → (propertyName → value), as the request sent it.</param>
    /// <param name="ct">The request's token.</param>
    public Task SaveAsync<T>(
        IAppDbContext db,
        T entity,
        Dictionary<string, Dictionary<string, string?>>? translations,
        CancellationToken ct)
        where T : class
    {
        if (translations is { Count: > 0 })
            multiLingual.For(entity).SetAll(ReadOnly(translations));

        return db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// As <see cref="SaveAsync"/>, for a new <paramref name="entity"/>: every base-language property
    /// the request left out is given <c>""</c>, which is what its column would hold anyway.
    ///
    /// <para><b>Why a create fills the gaps.</b> <c>Product</c> and <c>Category</c> declare their
    /// translatable properties as non-nullable strings defaulting to <c>string.Empty</c>. In
    /// base-language mode the library accepts a create under another language only when every
    /// translatable value the new entity holds came from <c>SetAll</c>, and it cannot tell a leftover
    /// <c>""</c> from a value. So a body with an English <c>Name</c> but no English
    /// <c>Description</c>, sent under <c>Accept-Language: ar</c>, would be refused
    /// (<c>BaseLanguageRequiredException</c>). Naming every property settles it — including a body
    /// with no base-language entry at all, which reads back as <c>""</c>.</para>
    ///
    /// <para><b>Why an update does not.</b> There, an unsent property holds whatever language the
    /// request read it in, and filling it would write that language into the base column.</para>
    ///
    /// <para>The request's own base-language values are kept as sent, unknown names included, so
    /// <c>SetAll</c> refuses a misspelled property here exactly as it does on an update.</para>
    /// </summary>
    /// <param name="db">The unit of work the row and its text are written in.</param>
    /// <param name="entity">A new entity, already added to <paramref name="db"/>.</param>
    /// <param name="translations">languageCode → (propertyName → value), as the request sent it.</param>
    /// <param name="ct">The request's token.</param>
    public Task CreateAsync<T>(
        IAppDbContext db,
        T entity,
        Dictionary<string, Dictionary<string, string?>>? translations,
        CancellationToken ct)
        where T : class
    {
        var text = ReadOnly(translations ?? []);

        // Inert without a base language: every language is a row, and an unset one is just absent.
        if (multiLingual.Languages.BaseLanguage is { } baseLanguage)
        {
            var values = text.TryGetValue(baseLanguage, out var given)
                ? new Dictionary<string, string?>(given, StringComparer.Ordinal)
                : new Dictionary<string, string?>(StringComparer.Ordinal);

            foreach (var property in MultiLingualMetadata.For(typeof(T)))
                values.TryAdd(property.Name, string.Empty);

            // The map ignores case, so this replaces the request's entry however it spelled the code.
            text[baseLanguage] = values;
        }

        if (text.Count > 0)
            multiLingual.For(entity).SetAll(text);

        return db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The request's map in the shape <c>SetAll</c> takes. Case-insensitive on the language code,
    /// because a code is the same language however a client spells it.
    /// </summary>
    private static Dictionary<string, IReadOnlyDictionary<string, string?>> ReadOnly(
        Dictionary<string, Dictionary<string, string?>> translations) =>
        translations.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyDictionary<string, string?>)kv.Value,
            StringComparer.OrdinalIgnoreCase);
}
