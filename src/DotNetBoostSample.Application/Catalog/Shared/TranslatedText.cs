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
/// <em>where does a language live?</em> Without
/// <c>MultiLingualOptions.BaseLanguage</c> the answer is "a translation row, always", and both
/// sides are a single library call. With one configured, one language is the entity's own columns
/// instead — and a reader has to know that on the way out as surely as a writer does on the way
/// in. Split across two classes, the second half of that was written twice and could be changed
/// once.</para>
///
/// <para><b>It is also the whole of what this application knows about the mode.</b> Four handlers
/// hand it the <c>translations</c> map the request arrived with, two hand it a query, and none of
/// them asks which mode is on — which is what keeps the slices identical in both and is the reason
/// this is a class rather than a few lines in each of them.</para>
///
/// <para><b>And one word of the name is deliberately loose.</b> In base-language mode the base
/// language's values are not a translation at all — they are the entity's own columns, and
/// <c>ITranslationAccessor.Set</c> refuses to be told otherwise. "Translated text" is what this
/// application calls a property whose value depends on the language, wherever the library keeps
/// it; the two stores are an implementation detail of that, and keeping them behind one name is
/// the whole reason a slice can ignore which mode is on.</para>
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
    /// languageCode → (propertyName → value).
    ///
    /// <para>This is the exception to "the interceptors already translated it". Everywhere else a
    /// plain <c>ToListAsync()</c> comes back in the request's language and that is the whole story;
    /// an editor draws a box per language, so it needs all of them at once.</para>
    ///
    /// <para>The query is taken rather than its result because, with a base language configured,
    /// WHICH language it is read in is part of the answer — see <see cref="InBaseLanguageAsync"/>.
    /// Nothing else in either detail response is affected by that: neither carries a localized
    /// name, and the alt text beside their images is read by the slice afterwards, in the request's
    /// own language.</para>
    /// </summary>
    /// <returns>The entity, or <c>null</c> when the query matched nothing — then the slice answers 404.</returns>
    public async Task<(T? Entity, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> Text)>
        LoadAsync<T>(IQueryable<T> one, CancellationToken ct)
        where T : class
    {
        var entity = await InBaseLanguageAsync(() => one.FirstOrDefaultAsync(ct), ct);

        if (entity is null) return (null, None);

        var stored = await multiLingual.For(entity).GetAllAsync(ct);

        // `stored` is every stored TRANSLATION. In base-language mode that is every language but
        // one, so the base language is put back beside them — otherwise the editor would open with
        // its boxes empty and save that emptiness over the canonical text on the first write.
        return (entity, BaseLanguage is { } code ? WithBaseValues(stored, code, entity) : stored);
    }

    /// <summary>
    /// Writes <paramref name="entity"/> and every language in <paramref name="translations"/> in
    /// one <c>SaveChanges</c> — the row and its text in the same transaction, which is why
    /// <c>Category</c> and <c>Product</c> are deliberately absent from <c>ExposeOverHttp</c>: a
    /// second way in would be a second answer.
    ///
    /// <para>Every language is queued with <c>Set</c>, by name because the property names arrive in
    /// a request body — except the base language, where there is nothing to queue: that language is
    /// the entity's own columns, and <c>Set</c> refuses to be told otherwise
    /// (<c>BaseLanguageNotTranslatableException</c>). It is assigned instead, and the save then runs
    /// in the language those values belong to.</para>
    /// </summary>
    /// <param name="db">The unit of work the row and its text are written in.</param>
    /// <param name="entity">A new or tracked entity carrying <c>[MultiLingual]</c> properties.</param>
    /// <param name="translations">languageCode → (propertyName → value), as the request sent it.</param>
    /// <param name="ct">The request's token.</param>
    public Task SaveAsync<T>(
        IAppDbContext db,
        T entity,
        Dictionary<string, Dictionary<string, string?>>? translations,
        CancellationToken ct)
        where T : class
    {
        var accessor = multiLingual.For(entity);

        foreach (var (language, values) in translations ?? [])
        {
            if (IsBase(language)) Assign(entity, values);
            else accessor.Set(language, values);
        }

        return InBaseLanguageAsync(() => db.SaveChangesAsync(ct), ct);
    }

    // ── Base-language mode ───────────────────────────────────────────────────
    // Everything below is inert without MultiLingualOptions.BaseLanguage: `BaseLanguage` is null,
    // `IsBase` is false for every code, and `InBaseLanguageAsync` runs the work as it is.

    /// <summary>The language kept in each entity's own columns, or null when every one is a row.</summary>
    private string? BaseLanguage => multiLingual.Languages.BaseLanguage;

    /// <summary>Whether <paramref name="code"/> is that language. False in the other mode, always.</summary>
    private bool IsBase(string code) => multiLingual.Languages.IsBaseLanguage(code);

    /// <summary>
    /// Runs <paramref name="work"/> with this scope reading and writing the BASE language, then
    /// puts the request's own language back.
    ///
    /// <para>It is the same detour for a read and for a write, because the base language is where
    /// the columns are read from and written to alike:</para>
    /// <list type="bullet">
    ///   <item><b>Reading</b>, a tracked entity's translatable properties hold whatever language the
    ///   query resolved — so one read under <c>Accept-Language: ar</c> has the Arabic name sitting
    ///   where the base value lives in the database. Reading in the base language is what makes
    ///   <c>entity.Name</c> the base value again.</item>
    ///   <item><b>Writing</b>, an entity may only be CREATED in the base language (the base value is
    ///   what every translation hangs off), and an assigned property goes to the column only while
    ///   that language is the one being authored. Otherwise it becomes a translation in whatever
    ///   language the request happened to be read in.</item>
    /// </list>
    ///
    /// <para><b>And why it is put back.</b> The endpoint answers with a sentence from
    /// <c>IStringLocalizer&lt;ApiMessages&gt;</c>, which reads <c>CurrentLanguage</c>, and so does
    /// the problem detail for a refused constraint — in a <c>finally</c>, so a failed save answers
    /// in the caller's language too. Leaving the scope in the base language would answer an Arabic
    /// request in English, which is exactly the kind of change to a response this mode is not
    /// allowed to make.</para>
    ///
    /// <para>A scope with no language resolved at all — a seeder, a background job — is left alone:
    /// the library exempts it from the create rule and its property values go straight to the
    /// columns.</para>
    /// </summary>
    private async Task<TResult> InBaseLanguageAsync<TResult>(Func<Task<TResult>> work, CancellationToken ct)
    {
        var requested = multiLingual.IsLanguageResolved ? multiLingual.CurrentLanguage.Code : null;

        if (BaseLanguage is not { } baseLanguage || requested is null || IsBase(requested))
            return await work();

        await multiLingual.UseLanguageAsync(baseLanguage, ct);

        try
        {
            return await work();
        }
        finally
        {
            await multiLingual.UseLanguageAsync(requested, ct);
        }
    }

    /// <summary>
    /// The stored translations plus the base language, taken off the entity's own properties —
    /// which is where that language lives, and which <see cref="InBaseLanguageAsync"/> has just
    /// made sure is what they hold.
    ///
    /// <para>Read after that method put the request's language back, because what it restores is a
    /// SCOPE: it does not touch an entity already in memory.</para>
    ///
    /// <para>The map is case-insensitive on the way in, because its two halves name the language
    /// independently — the stored keys come from the translation rows and this one from the
    /// configured option. Both are canonical today, and a map carrying <c>en</c> beside <c>EN</c>
    /// would be a shape no client should have to read.</para>
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> WithBaseValues<T>(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> stored,
        string baseLanguage,
        T entity)
        where T : class
    {
        var text = new Dictionary<string, IReadOnlyDictionary<string, string?>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var (code, inLanguage) in stored) text[code] = inLanguage;

        text[baseLanguage] = MultiLingualMetadata.For(typeof(T))
            .ToDictionary(property => property.Name, property => property.GetValue(entity),
                StringComparer.Ordinal);

        return text;
    }

    /// <summary>
    /// Assigns the base language's values to those same properties.
    ///
    /// <para>Matched against <c>MultiLingualMetadata</c> rather than assigned to whatever the body
    /// happens to name, so a property the entity does not declare as translatable is refused here
    /// exactly as <c>Set</c> refuses it in the other mode: a caller cannot tell from a refusal which
    /// mode the server is in.</para>
    ///
    /// <para><c>null</c> becomes the empty string, because these properties are non-nullable
    /// <c>string</c>s and their columns are NOT NULL. Clearing the base language's text leaves an
    /// empty value rather than removing a row — the honest difference between a column and a
    /// translation is that there is nothing to delete.</para>
    /// </summary>
    private static void Assign<T>(T entity, Dictionary<string, string?> values)
        where T : class
    {
        var translatable = MultiLingualMetadata.For(typeof(T));

        foreach (var (name, value) in values)
        {
            var property =
                translatable.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException(
                    $"'{name}' is not a translatable property of {typeof(T).Name}.", nameof(values));

            property.SetValue(entity, value ?? string.Empty);
        }
    }
}
