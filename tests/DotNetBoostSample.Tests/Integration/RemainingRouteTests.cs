using System.Net;
using Xunit;

namespace DotNetBoostSample.Tests.Integration;

/// <summary>
/// The routes nothing else in this suite called.
///
/// <para><b>Why they are collected rather than distributed.</b> They were found by listing the
/// generated OpenAPI document and crossing it against what the tests actually request — six of
/// forty-five, each one reachable from the dashboard or from a client reading the document, and
/// none of them asserted anywhere. A test per route is worth more than a tidy home for it, and together
/// they are the difference between "the suite is green" and "every published route has been
/// called".</para>
///
/// <para><b>And the forty-five is pinned rather than remembered.</b> A cross-check done by hand
/// holds only until the next route is mapped — by this application or by the next version of a
/// library whose endpoints it publishes. <c>OpenApiShapeTests</c> asserts the inventory by name, so
/// a new route arrives as a failing test with that route in the message, which is the prompt to
/// call it here.</para>
///
/// <para>Like every other class here, this one runs in both modes. Two of the six are the reason
/// that matters: promoting a language to default while a base language exists, and reading a
/// subject's values with the fallback — the first because the default and the base language are
/// different things that a reader could easily assume are one, and the second because a subject is
/// precisely the case where the base language stays a translation row.</para>
/// </summary>
public abstract class RemainingRouteTests(SampleApp app)
{
    /// <summary>
    /// <c>POST /api/languages/{code}/set-default</c> — the dashboard's star button.
    ///
    /// <para>The default is a row in the languages table and moves freely; the base language is a
    /// property of the schema and does not. Moving the default while a base language is configured
    /// therefore has to be allowed, and has to leave the columns alone — which is the whole reason
    /// the library keeps them as two ideas rather than one.</para>
    ///
    /// <para>Restored in a <c>finally</c>: the tests in a collection share one database, and a
    /// default language left somewhere else would change what every later fallback resolves to.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task The_default_language_can_be_moved_and_moved_back()
    {
        var client = app.Client;

        try
        {
            await client.SendJsonOkAsync(HttpMethod.Post, "/api/languages/fr/set-default");

            var languages = await client.GetJsonAsync("/api/languages");

            Assert.Equal("fr", languages.EnumerateArray().Single(l => l.GetProperty("isDefault").GetBoolean()).Str("code"));

            // The seeded English text is still there to be read, whichever row carries the flag.
            Assert.Equal(
                "Oak writing desk",
                (await client.GetJsonAsync("/api/products", "en"))
                    .GetProperty("items").EnumerateArray().First(p => p.Str("sku") == "DESK-001").Str("name"));
        }
        finally
        {
            await client.SendJsonOkAsync(HttpMethod.Post, "/api/languages/en/set-default");
        }

        var restored = await client.GetJsonAsync("/api/languages");

        Assert.Equal("en", restored.EnumerateArray().Single(l => l.GetProperty("isDefault").GetBoolean()).Str("code"));
    }

    /// <summary>
    /// <c>GET /api/translations/{entityName}/{entityId}/resolved</c> and
    /// <c>POST /api/translations/{entityName}/resolve</c> — one subject's values in the request's
    /// language, and many subjects' in one query.
    ///
    /// <para>The same two reads <c>MediaText</c> makes in process, over HTTP. A subject has no
    /// columns, so every language it carries is a translation row in both modes — including the base
    /// language, which is exactly what the alt-text editor depends on.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_subjects_values_resolve_one_at_a_time_and_in_bulk()
    {
        var client = app.Client;

        var productId = await client.CreateProductAsync(app.SeededCategoryId, "Product with resolvable text");
        var fileId = (await client.UploadAsync($"/api/products/{productId}/images?role=primary", "resolve.png")).Str("id");

        await client.SendJsonOkAsync(HttpMethod.Put, $"/api/translations/MediaFile/{fileId}", new
        {
            translations = new Dictionary<string, Dictionary<string, string?>>
            {
                ["en"] = new() { ["AltText"] = "A resolvable file" },
                ["ar"] = new() { ["AltText"] = "ملف قابل للحل" }
            }
        });

        // One subject, in each language, with the fallback for the one nobody wrote. The values
        // travel under `values`, beside the subject's own address — so a caller knows which
        // language it was answered in rather than having to remember what it asked for.
        async Task<string> ResolvedAsync(string language) =>
            (await client.GetJsonAsync($"/api/translations/MediaFile/{fileId}/resolved", language))
                .GetProperty("values").Str("AltText");

        Assert.Equal("A resolvable file", await ResolvedAsync("en"));
        Assert.Equal("ملف قابل للحل", await ResolvedAsync("ar"));
        Assert.Equal("A resolvable file", await ResolvedAsync("fr"));

        // And many at once — the bulk form a listing needs, which is what MediaText.GetManyAsync
        // calls in process, keyed by the id each answer belongs to.
        var bulk = await client.SendJsonCheckedAsync(HttpMethod.Post, "/api/translations/MediaFile/resolve",
            new { entityIds = new[] { fileId } }, language: "ar");

        Assert.Equal("ar", bulk.Str("languageCode"));
        Assert.Equal("ملف قابل للحل", bulk.GetProperty("subjects").GetProperty(fileId).Str("AltText"));

        await client.SendJsonOkAsync(HttpMethod.Delete, $"/api/products/{productId}");
    }

    /// <summary>
    /// <c>PUT /api/media/attachments/order</c> and
    /// <c>DELETE /api/media/{id}/attachments/{attachmentId}</c> — reordering a role's files and
    /// detaching one.
    ///
    /// <para>Nothing here is translated, which is the assertion: a gallery reordered and an
    /// attachment removed behave the same whichever mode the application is in, and the file itself
    /// survives being detached — the media library's own rule, and the one that lets a logo be
    /// detached from branding without deleting the image.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_role_can_be_reordered_and_an_attachment_removed()
    {
        var client = app.Client;

        var productId = await client.CreateProductAsync(app.SeededCategoryId, "Product with a gallery");

        var first = (await client.UploadAsync($"/api/products/{productId}/images?role=gallery", "one.png")).Str("id");
        var second = (await client.UploadAsync($"/api/products/{productId}/images?role=gallery", "two.png")).Str("id");

        string[] GalleryOrder(System.Text.Json.JsonElement detail) =>
            [.. detail.GetProperty("images").EnumerateArray()
                .Where(i => i.Str("role") == "gallery")
                .OrderBy(i => i.Int("sortOrder"))
                .Select(i => i.Str("id"))];

        Assert.Equal([first, second], GalleryOrder(await client.GetJsonAsync($"/api/products/{productId}")));

        await client.SendJsonOkAsync(HttpMethod.Put, "/api/media/attachments/order", new
        {
            ownerEntityName = "Product",
            ownerEntityId = productId.ToString(),
            role = "gallery",
            mediaFileIds = new[] { second, first }
        });

        Assert.Equal([second, first], GalleryOrder(await client.GetJsonAsync($"/api/products/{productId}")));

        // Detach the one now in front. An attachment has an id of its own — the file is attached
        // rather than owned, and may be attached several times over — and it is the file's own
        // record that carries it, not the owner's view of it.
        var attachmentId = (await client.GetJsonAsync($"/api/media/{second}"))
            .GetProperty("attachments").EnumerateArray()
            .Single(a => a.Str("ownerEntityId") == productId.ToString() && a.Str("role") == "gallery")
            .Str("id");

        await client.SendJsonOkAsync(HttpMethod.Delete, $"/api/media/{second}/attachments/{attachmentId}");

        Assert.Equal([first], GalleryOrder(await client.GetJsonAsync($"/api/products/{productId}")));

        var file = await client.GetAsync($"/api/media/{second}");

        Assert.True(file.IsSuccessStatusCode, "detaching a file must not delete it");

        await client.SendJsonOkAsync(HttpMethod.Delete, $"/api/products/{productId}");
    }

    /// <summary>
    /// <c>GET /api/media/{id}/url</c> — a direct link to the bytes when the backend mints one.
    ///
    /// <para>This sample stores bytes on local disk, which mints none, so the honest answer is
    /// "no link" rather than an error. Asserted because it is the reason every shape this API sends
    /// carries an id instead of a URL, and the reason the dashboard derives
    /// <c>/api/media/{id}/content</c> from that id — a client that trusted this field would render
    /// nothing here and break on a switch to Azure or S3, where the link exists but expires.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_filesystem_backed_file_mints_no_direct_url()
    {
        var client = app.Client;

        var productId = await client.CreateProductAsync(app.SeededCategoryId, "Product with a local file");
        var fileId = (await client.UploadAsync($"/api/products/{productId}/images?role=primary", "local.png")).Str("id");

        var response = await client.GetAsync($"/api/media/{fileId}/url");

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();

            Assert.True(
                body.Contains("null", StringComparison.OrdinalIgnoreCase) || body.Length == 0,
                $"the FileSystem backend should mint no URL, but answered: {body}");
        }

        // The bytes are reachable all the same, from the id every shape does carry.
        var content = await client.GetAsync($"/api/media/{fileId}/content");

        Assert.True(content.IsSuccessStatusCode);

        await client.SendJsonOkAsync(HttpMethod.Delete, $"/api/products/{productId}");
    }
}

/// <summary>
/// <inheritdoc cref="RemainingRouteTests" path="/summary/node()[1]"/>
/// <para>Run in the mode the sample ships in: every language is a translation row.</para>
/// </summary>
[Collection(SampleAppCollection.Name)]
public sealed class RemainingRouteTestsInDefaultMode(SampleApp app) : RemainingRouteTests(app);

/// <summary>
/// The same routes against the same application booted with
/// <c>MultiLingualOptions.BaseLanguage = "en"</c>.
/// </summary>
[Collection(BaseLanguageSampleAppCollection.Name)]
public sealed class RemainingRouteTestsInBaseLanguageMode(BaseLanguageSampleApp app) : RemainingRouteTests(app);
