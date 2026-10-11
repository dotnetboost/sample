using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Metadata;
using DotNetBoostSample.Application.Abstractions.Persistence;

namespace DotNetBoostSample.Application.Catalog.Shared;

/// <summary>
/// Writing per-language text alongside a row, shared by the four slices that take it: creating and
/// updating a category, and creating and updating a product.
///
/// <para>Uses the by-name overload of <c>Set</c> because the property names arrive in a request
/// body rather than being known at compile time. This is also why <c>Category</c> and
/// <c>Product</c> are deliberately absent from <c>ExposeOverHttp</c>: their text is written here,
/// in the same transaction as the slug or the SKU, and a second way in would be a second
/// answer.</para>
///
/// <para><b>Why this owns the <c>SaveChanges</c>.</b> It used to queue the values and leave the
/// save to the handler, which is the whole story in the mode this sample runs in by default. With
/// <c>MultiLingualOptions.BaseLanguage</c> configured it is not: one of the languages in the
/// request body is no longer a translation at all, it is the entity's own columns, and whether a
/// column or a row is written is decided by the language the save runs in. Queueing and saving are
/// therefore one operation, and the handlers call this instead of <c>db.SaveChangesAsync</c>.</para>
///
/// <para><b>What a caller does not have to know.</b> Which mode is on. The four slices hand the
/// request's <c>translations</c> map straight through in both, and this file is one of the two that
/// ask — <see cref="TranslationReads"/> is the other.</para>
/// </summary>
public static class TranslationWrites
{
    /// <summary>
    /// Writes <paramref name="entity"/> and every language of text in <paramref name="translations"/>
    /// in one <c>SaveChanges</c>.
    ///
    /// <para><b>Without a base language</b> — the default mode — every language is a translation
    /// row, so every entry is queued with <c>Set</c> and the save is an ordinary one. MultiLingual's
    /// interceptor writes the queued rows in the same transaction as the insert or update.</para>
    ///
    /// <para><b>With one</b>, the entry naming it is assigned to the entity's own translatable
    /// properties — that language IS those columns, and <c>Set</c> refuses to be told otherwise
    /// (<c>BaseLanguageNotTranslatableException</c>) — and the others are queued as before. The save
    /// then runs in the base language, for two reasons the library spells out:</para>
    /// <list type="bullet">
    ///   <item>An entity may only be CREATED in the base language, because the base value is what
    ///   every translation hangs off. A create under <c>Accept-Language: ar</c> would otherwise be
    ///   refused with <c>BaseLanguageRequiredException</c> — a 500 on this application's own
    ///   endpoints, which have no filter mapping the library's exceptions.</item>
    ///   <item>An assigned translatable property goes to the column when the base language is the
    ///   one being authored and to a translation row when it is not. The values assigned just above
    ///   are the base language's, so the save has to be the base language's too.</item>
    /// </list>
    ///
    /// <para>The request's own language is put back afterwards, in a <c>finally</c>: the endpoint
    /// answers with a sentence from <c>IStringLocalizer&lt;ApiMessages&gt;</c>, which reads
    /// <c>CurrentLanguage</c>, and so does the problem detail for a refused constraint. Leaving the
    /// scope in the base language would answer an Arabic request in English — which is exactly the
    /// kind of change to a response this mode is not allowed to make.</para>
    /// </summary>
    /// <param name="db">The unit of work the row and its text are written in.</param>
    /// <param name="multiLingual">The engine — asked which mode it is in rather than told.</param>
    /// <param name="entity">A new or tracked entity carrying <c>[MultiLingual]</c> properties.</param>
    /// <param name="translations">languageCode → (propertyName → value), as the request sent it.</param>
    /// <param name="ct">The request's token.</param>
    public static async Task SaveAsync<T>(
        IAppDbContext db,
        IMultiLingualManager multiLingual,
        T entity,
        Dictionary<string, Dictionary<string, string?>>? translations,
        CancellationToken ct)
        where T : class
    {
        var accessor = multiLingual.For(entity);
        var baseLanguage = multiLingual.Languages.BaseLanguage;

        foreach (var (language, values) in translations ?? [])
        {
            if (multiLingual.Languages.IsBaseLanguage(language))
                AssignBaseValues(entity, values);
            else
                accessor.Set(language, values);
        }

        // Outside base-language mode there is nothing to author in a particular language: every
        // value in the map was queued above and the interceptor files all of them as rows.
        if (baseLanguage is null)
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var requested = multiLingual.IsLanguageResolved ? multiLingual.CurrentLanguage.Code : null;

        // Already right: a request read in the base language, or a scope with no language resolved
        // at all — a seeder or a background job, which the library exempts from the create rule
        // because its property values go straight to the columns.
        if (requested is null || multiLingual.Languages.IsBaseLanguage(requested))
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        await multiLingual.UseLanguageAsync(baseLanguage, ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            await multiLingual.UseLanguageAsync(requested, ct);
        }
    }

    /// <summary>
    /// Assigns the base language's values to the entity's own properties — the columns
    /// <c>ApplyMultiLingual</c> leaves in place when a base language is configured.
    ///
    /// <para>Property names are matched against <c>MultiLingualMetadata</c> rather than assigned by
    /// reflection on anything that happens to be in the body, so a name the entity does not declare
    /// as translatable is refused here exactly as <c>Set</c> would refuse it in the other mode. The
    /// two paths fail the same way, which is the point: a caller cannot tell from a refusal which
    /// mode the server is in.</para>
    ///
    /// <para><c>null</c> becomes the empty string because a translatable property on these entities
    /// is a non-nullable <c>string</c> and its column is NOT NULL. Clearing the base language's text
    /// therefore leaves an empty value rather than removing a row, which is the honest difference
    /// between a column and a translation: there is nothing to delete.</para>
    /// </summary>
    private static void AssignBaseValues<T>(T entity, Dictionary<string, string?> values)
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
