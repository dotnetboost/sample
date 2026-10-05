using System.Net;
using Xunit;

namespace DotNetBoostSample.Tests.Integration;

/// <summary>
/// What the catalogue endpoints get from the libraries without asking: entities translated by
/// MultiLingual's EF interceptors, alt text that is MultiLingual rows about a Media file, and the
/// API's own sentences answered in the language the content was read in.
/// </summary>
[Collection(SampleAppCollection.Name)]
public class CatalogCompositionTests(SampleApp app)
{
    /// <summary>
    /// The claim the README opens with: <c>db.Products.ToListAsync()</c> comes back translated
    /// because the interceptors did it, with no join and no explicit read in the endpoint.
    /// </summary>
    [RequiresDockerFact]
    public async Task A_plain_entity_read_comes_back_in_the_requests_language()
    {
        var client = app.Client;

        string NameOf(System.Text.Json.JsonElement page) =>
            page.GetProperty("items").EnumerateArray().First(p => p.Str("sku") == "DESK-001").Str("name");

        Assert.Equal("Oak writing desk", NameOf(await client.GetJsonAsync("/api/products", "en")));
        Assert.Equal("مكتب كتابة من البلوط", NameOf(await client.GetJsonAsync("/api/products", "ar")));
        Assert.Equal("Bureau en chêne", NameOf(await client.GetJsonAsync("/api/products", "fr")));

        // The SKU is [NotMultiLingual] — a column, the same string in every language.
        var arabic = await client.GetJsonAsync("/api/products", "ar");
        Assert.Contains(arabic.GetProperty("items").EnumerateArray(), p => p.Str("sku") == "DESK-001");
    }

    /// <summary>
    /// A language with no translation falls back to the default rather than coming back empty —
    /// the rule that makes adding a language safe before anyone has translated anything.
    /// </summary>
    [RequiresDockerFact]
    public async Task An_untranslated_language_falls_back_to_the_default()
    {
        var client = app.Client;

        var created = await client.SendJsonCheckedAsync(HttpMethod.Post, "/api/categories", new
        {
            slug = $"fallback-{Guid.NewGuid():N}"[..20],
            translations = new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["Name"] = "English only", ["Description"] = "No other language." }
            }
        });

        var id = created.Int("id");

        var inFrench = (await client.GetJsonAsync("/api/categories", "fr"))
            .EnumerateArray().First(c => c.Int("id") == id);

        Assert.Equal("English only", inFrench.Str("name"));

        await client.SendJsonAsync(HttpMethod.Delete, $"/api/categories/{id}");
    }

    /// <summary>
    /// The composition in <c>Integration/MediaText.cs</c>: alt text is a MultiLingual row filed
    /// under the subject <c>MediaFile</c> and a Media file's own GUID. Neither library knows, and
    /// the two line up only because both format the key the same way.
    /// </summary>
    [RequiresDockerFact]
    public async Task Alt_text_written_as_translations_reads_back_beside_the_file_it_describes()
    {
        var client = app.Client;

        var productId = await client.CreateProductAsync("Desk with alt text");

        var upload = await client.UploadAsync($"/api/products/{productId}/images?role=primary", "desk.png");
        var fileId = upload.Str("id");

        await client.SendJsonCheckedAsync(HttpMethod.Post, $"/api/media-text/{fileId}", new
        {
            values = new Dictionary<string, object>
            {
                ["en"] = new { altText = "An oak writing desk", caption = "Front view" },
                ["ar"] = new { altText = "مكتب كتابة من البلوط", caption = "من الأمام" }
            }
        });

        // Read through the catalogue, not through the media endpoints: the product's image carries
        // the alt text for the language the product itself was read in.
        async Task<string> AltForAsync(string language)
        {
            var page = await client.GetJsonAsync($"/api/products?pageSize=200", language);
            var product = page.GetProperty("items").EnumerateArray().First(p => p.Int("id") == productId);
            return product.GetProperty("primary").Str("altText");
        }

        Assert.Equal("An oak writing desk", await AltForAsync("en"));
        Assert.Equal("مكتب كتابة من البلوط", await AltForAsync("ar"));

        // French has no row of its own, so it falls back to the default language.
        Assert.Equal("An oak writing desk", await AltForAsync("fr"));

        await client.SendJsonAsync(HttpMethod.Delete, $"/api/products/{productId}");
    }

    /// <summary>
    /// The API's own sentences are code rather than content, so no interceptor touches them — but
    /// they answer in <c>IMultiLingualManager.CurrentLanguage</c> all the same, so a refusal and
    /// the content it was about cannot disagree about what language the caller reads.
    /// </summary>
    [RequiresDockerFact]
    public async Task A_refusal_is_written_in_the_language_the_content_was_read_in()
    {
        var client = app.Client;

        var english = await client.SendJsonAsync(HttpMethod.Delete, "/api/categories/999999", language: "en");
        var french = await client.SendJsonAsync(HttpMethod.Delete, "/api/categories/999999", language: "fr");
        var arabic = await client.SendJsonAsync(HttpMethod.Delete, "/api/categories/999999", language: "ar");

        Assert.True(english.IsNotFound());

        Assert.Equal("Category 999999 was not found.", await english.ProblemDetailAsync());
        Assert.Equal("La catégorie 999999 est introuvable.", await french.ProblemDetailAsync());
        Assert.Equal("التصنيف 999999 غير موجود.", await arabic.ProblemDetailAsync());
    }

    /// <summary>
    /// A write that succeeded says so, naming the row by the handle the editor typed rather than
    /// by the id the request used — and answers 200 rather than 204 so there is somewhere to put
    /// the sentence.
    /// </summary>
    [RequiresDockerFact]
    public async Task A_write_that_succeeded_says_what_it_did_and_names_the_row()
    {
        var client = app.Client;
        var slug = $"said-{Guid.NewGuid():N}"[..16];

        var created = await client.SendJsonCheckedAsync(HttpMethod.Post, "/api/categories",
            new { slug, translations = new Dictionary<string, Dictionary<string, string>>() }, "fr");

        Assert.Equal($"La catégorie {slug} a été créée.", created.Str("message"));

        var deleted = await client.SendJsonAsync(HttpMethod.Delete, $"/api/categories/{created.Int("id")}", language: "ar");

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal($"تم حذف التصنيف {slug}.", (await deleted.ReadJsonAsync("the delete")).Str("message"));
    }

    /// <summary>
    /// Deleting a product takes its files AND their alt text, which nothing cascades — the media
    /// library has never heard of the translation rows, so the endpoint writes the cleanup out.
    /// </summary>
    [RequiresDockerFact]
    public async Task Deleting_a_product_removes_the_alt_text_nothing_cascades()
    {
        var client = app.Client;

        var product = await client.SendJsonCheckedAsync(HttpMethod.Post, "/api/products", new
        {
            sku = $"TMP-{Guid.NewGuid():N}"[..12],
            price = 10.0m,
            categoryId = 1,
            translations = new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["Name"] = "Doomed", ["Description"] = "Briefly here." }
            }
        });

        var productId = product.Int("id");

        var upload = await client.UploadAsync($"/api/products/{productId}/images?role=primary");
        var fileId = Guid.Parse(upload.Str("id"));

        await client.SendJsonCheckedAsync(HttpMethod.Post, $"/api/media-text/{fileId}",
            new { values = new Dictionary<string, object> { ["en"] = new { altText = "Gone soon", caption = (string?)null } } });

        await app.WithDbAsync(async db =>
            Assert.True(await TranslationCountAsync(db, fileId) > 0, "the alt text was never written"));

        var deleted = await client.SendJsonAsync(HttpMethod.Delete, $"/api/products/{productId}");
        await deleted.EnsureOkAsync("the product delete");

        // The product's own translations go through MultiLingual's SaveChanges interceptor; the
        // file's alt text does not, and is the part CatalogEndpoints has to remove by hand.
        await app.WithDbAsync(async db =>
            Assert.Equal(0, await TranslationCountAsync(db, fileId)));
    }

    private static async Task<int> TranslationCountAsync(Api.Data.AppDbContext db, Guid fileId)
    {
        var key = fileId.ToString("D");

        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
            db.Set<DotNetBoost.MultiLingual.Core.Models.Translation>()
                .Where(t => t.EntityName == "MediaFile" && t.EntityId == key));
    }
}
