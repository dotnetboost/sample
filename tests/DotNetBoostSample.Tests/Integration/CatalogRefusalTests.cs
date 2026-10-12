using System.Net;
using Xunit;

namespace DotNetBoostSample.Tests.Integration;

/// <summary>
/// What the catalogue says no to, and in what language.
///
/// <para><b>Why this file exists.</b> Every test here covers a request that used to be answered
/// with <c>500 An error occurred while processing your request</c> — not because anything was
/// unhandled in the usual sense, but because nothing had decided what the right answer was. Two
/// classes of them: a body whose shape is wrong, which reached the caller as a 500 until
/// <c>ValidationExceptionHandler</c> existed, and a body naming a category that is not there,
/// which the foreign key refused and nobody translated.</para>
///
/// <para>Neither was visible to the suite as it stood, because no test sent an invalid body. They
/// were found by running the application and asking it. That is the gap this file closes: a
/// refusal is part of an API's contract, and an untested refusal is a 500 waiting to be
/// discovered by whoever is using it.</para>
/// </summary>
public abstract class CatalogRefusalTests(SampleApp app)
{
    /// <summary>A category id nothing points at is the caller's mistake, and is named as such.</summary>
    [RequiresDockerFact]
    public async Task Creating_a_product_in_a_category_that_does_not_exist_is_refused()
    {
        var response = await app.Client.SendJsonAsync(HttpMethod.Post, "/api/products", new
        {
            sku = $"NOCAT-{Guid.NewGuid():N}"[..12],
            price = 1.0m,
            categoryId = 999_999,
            translations = new Dictionary<string, Dictionary<string, string>>()
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // The id is in the sentence, which is the whole reason the handler asks rather than
        // letting the foreign key answer: a translated 23503 could not name it.
        Assert.Contains("999999", await response.ProblemDetailAsync());
    }

    /// <summary>And the same on the way through — a PUT may move a product anywhere.</summary>
    [RequiresDockerFact]
    public async Task Moving_a_product_into_a_category_that_does_not_exist_is_refused()
    {
        var client = app.Client;
        var id = await client.CreateProductAsync("Product that stays put");

        var response = await client.SendJsonAsync(HttpMethod.Put, $"/api/products/{id}", new
        {
            sku = $"MOVED-{Guid.NewGuid():N}"[..12],
            price = 2.0m,
            categoryId = 999_999,
            translations = new Dictionary<string, Dictionary<string, string>>()
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // And the product is untouched: the check runs before anything is assigned.
        Assert.Equal("Product that stays put", (await client.GetJsonAsync($"/api/products/{id}"))
            .GetProperty("translations").GetProperty("en").Str("Name"));
    }

    /// <summary>
    /// The refusal answers in the request's language, like every other one — so a caller showing
    /// it to a user does not have to translate it first.
    /// </summary>
    [RequiresDockerFact]
    public async Task A_missing_category_is_refused_in_the_requests_language()
    {
        var response = await app.Client.SendJsonAsync(HttpMethod.Post, "/api/products", new
        {
            sku = $"AR-{Guid.NewGuid():N}"[..12],
            price = 1.0m,
            categoryId = 999_999,
            translations = new Dictionary<string, Dictionary<string, string>>()
        }, language: "ar");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var detail = await response.ProblemDetailAsync();

        Assert.Contains("999999", detail);
        Assert.DoesNotContain("was not found", detail);
    }

    /// <summary>
    /// A body whose shape is wrong is a 400 naming the field — not the 500 it was before
    /// <c>ValidationExceptionHandler</c> turned FluentValidation's exception into a response.
    /// </summary>
    [RequiresDockerFact]
    public async Task An_invalid_body_is_refused_with_the_failing_fields()
    {
        var response = await app.Client.SendJsonAsync(HttpMethod.Post, "/api/products", new
        {
            sku = "",
            price = -5.0m,
            categoryId = 1,
            translations = new Dictionary<string, Dictionary<string, string>>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = (await response.ProblemJsonAsync()).GetProperty("errors");

        // Camel-cased, so a field is named the way the caller sent it rather than the way C#
        // spells it.
        Assert.True(errors.TryGetProperty("sku", out _), "the empty sku should be named");
        Assert.True(errors.TryGetProperty("price", out _), "the negative price should be named");
    }

    /// <summary>
    /// Two categories cannot share a slug, and the refusal says which field — a <c>409</c> from
    /// the unique index, translated by <c>PostgresErrorTranslator</c>, where it used to be a 500.
    /// </summary>
    [RequiresDockerFact]
    public async Task A_duplicate_slug_is_refused_as_a_conflict()
    {
        var client = app.Client;
        var slug   = $"dup-{Guid.NewGuid():N}"[..12];

        await client.SendJsonCheckedAsync(HttpMethod.Post, "/api/categories", new
        {
            slug,
            translations = new Dictionary<string, Dictionary<string, string>>()
        });

        var again = await client.SendJsonAsync(HttpMethod.Post, "/api/categories", new
        {
            slug,
            translations = new Dictionary<string, Dictionary<string, string>>()
        });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("slug", await again.ProblemDetailAsync(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The same on the product side, where the index is on the SKU.</summary>
    [RequiresDockerFact]
    public async Task A_duplicate_sku_is_refused_as_a_conflict()
    {
        var client = app.Client;
        var sku    = $"DUP-{Guid.NewGuid():N}"[..12];

        await client.SendJsonCheckedAsync(HttpMethod.Post, "/api/products", new
        {
            sku,
            price = 1.0m,
            categoryId = 1,
            translations = new Dictionary<string, Dictionary<string, string>>()
        });

        var again = await client.SendJsonAsync(HttpMethod.Post, "/api/products", new
        {
            sku,
            price = 1.0m,
            categoryId = 1,
            translations = new Dictionary<string, Dictionary<string, string>>()
        });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("SKU", await again.ProblemDetailAsync(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// And it answers in the request's language, which is the point of routing it through the same
    /// <c>Error</c> every handwritten refusal uses rather than letting it escape as a 500.
    /// </summary>
    [RequiresDockerFact]
    public async Task A_duplicate_slug_is_refused_in_the_requests_language()
    {
        var client = app.Client;
        var slug   = $"dupar-{Guid.NewGuid():N}"[..12];

        await client.SendJsonCheckedAsync(HttpMethod.Post, "/api/categories", new
        {
            slug,
            translations = new Dictionary<string, Dictionary<string, string>>()
        });

        var again = await client.SendJsonAsync(HttpMethod.Post, "/api/categories", new
        {
            slug,
            translations = new Dictionary<string, Dictionary<string, string>>()
        }, language: "fr");

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("déjà", await again.ProblemDetailAsync());
    }

    /// <summary>An empty slug is the same story on the category side.</summary>
    [RequiresDockerFact]
    public async Task An_invalid_category_body_is_refused_with_the_failing_field()
    {
        var response = await app.Client.SendJsonAsync(HttpMethod.Post, "/api/categories", new
        {
            slug = "",
            translations = new Dictionary<string, Dictionary<string, string>>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(
            (await response.ProblemJsonAsync()).GetProperty("errors").TryGetProperty("slug", out _),
            "the empty slug should be named");
    }
}

/// <summary>
/// <inheritdoc cref="CatalogRefusalTests" path="/summary/node()[1]"/>
/// <para>Run in the mode the sample ships in: every language is a translation row.</para>
/// </summary>
[Collection(SampleAppCollection.Name)]
public sealed class CatalogRefusalTestsInDefaultMode(SampleApp app) : CatalogRefusalTests(app);

/// <summary>
/// The same assertions about every refusal this API answers, against the same application booted with
/// <c>MultiLingualOptions.BaseLanguage = "en"</c>.
///
/// <para>Running them twice rather than reasoning about which ones could care is the point: the
/// mode moves content between a column and a translation row, and the claim this sample makes is
/// that no caller can tell. A claim like that is worth more re-run than re-read.</para>
/// </summary>
[Collection(BaseLanguageSampleAppCollection.Name)]
public sealed class CatalogRefusalTestsInBaseLanguageMode(BaseLanguageSampleApp app) : CatalogRefusalTests(app);
