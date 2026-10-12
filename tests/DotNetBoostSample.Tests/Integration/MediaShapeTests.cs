using DotNetBoostSample.Application.Composition;
using Xunit;
using DotNetBoostSample.Infrastructure.Persistence;

namespace DotNetBoostSample.Tests.Integration;

/// <summary>
/// What the API says about a file, and the distinction it has to keep: a FILE, which a listing is
/// about, and a file IN A ROLE, which an editor is about. The media library draws the same line
/// between <c>ListAsync</c> and <c>ListWithRolesAsync</c>.
/// </summary>
public abstract class MediaShapeTests(SampleApp app)
{
    /// <summary>
    /// The case that separates the two shapes, and the regression test for 9977507: one file
    /// attached twice over. The listing must count it once and report no role; the editor must see
    /// both slots.
    /// </summary>
    [RequiresDockerFact]
    public async Task One_file_in_two_roles_is_one_image_in_the_list_and_two_boxes_in_the_editor()
    {
        var client = app.Client;

        var product = await client.SendJsonCheckedAsync(HttpMethod.Post, "/api/products", new
        {
            sku = $"TWO-{Guid.NewGuid():N}"[..12],
            price = 1.0m,
            categoryId = 1,
            translations = new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["Name"] = "Twice attached", ["Description"] = "One file, two roles." }
            }
        });

        var productId = product.Int("id");

        var upload = await client.UploadAsync($"/api/products/{productId}/images?role=primary");
        var fileId = upload.Str("id");

        // The same file again, now as a gallery image.
        await client.SendJsonCheckedAsync(HttpMethod.Post, $"/api/media/{fileId}/attachments", new
        {
            ownerEntityName = "Product",
            ownerEntityId = productId.ToString(),
            role = "gallery",
            replace = false
        });

        // ── The listing: one image, and no role on it ────────────────────────
        var listed = (await client.GetJsonAsync("/api/products?pageSize=200"))
            .GetProperty("items").EnumerateArray().First(p => p.Int("id") == productId);

        var images = listed.GetProperty("images").EnumerateArray().ToList();

        Assert.Single(images);
        Assert.Equal(fileId, images[0].Str("id"));

        // `role: ""` used to be here, in the same field name the detail endpoint fills properly.
        Assert.DoesNotContain("role", images[0].Keys());
        Assert.DoesNotContain("sortOrder", images[0].Keys());

        // The thumbnail is the same shape — the field it sits in already says which role it is.
        Assert.DoesNotContain("role", listed.GetProperty("primary").Keys());

        // ── The detail: one entry per role ───────────────────────────────────
        var detail = await client.GetJsonAsync($"/api/products/{productId}");
        var slots = detail.GetProperty("images").EnumerateArray().ToList();

        Assert.Equal(2, slots.Count);
        Assert.All(slots, slot => Assert.Equal(fileId, slot.Str("id")));
        Assert.Equal(["gallery", "primary"], slots.Select(s => s.Str("role")).Order());

        await client.SendJsonAsync(HttpMethod.Delete, $"/api/products/{productId}");
    }

    /// <summary>
    /// No shape of ours carries a URL. The route is derived from the id, in one place, because the
    /// media library's own <c>url</c> is a signed link that is null on the FileSystem backend —
    /// so a second, differently-built one from here would only be something to drift.
    /// </summary>
    [RequiresDockerFact]
    public async Task An_image_carries_no_url_but_its_bytes_are_reachable_from_its_id()
    {
        var client = app.Client;

        var productId = await client.CreateProductAsync("Product with a reachable image");

        var upload = await client.UploadAsync($"/api/products/{productId}/images?role=primary");
        var fileId = upload.Str("id");

        var listed = (await client.GetJsonAsync("/api/products?pageSize=200"))
            .GetProperty("items").EnumerateArray().First(p => p.Int("id") == productId);

        Assert.DoesNotContain("url", listed.GetProperty("primary").Keys());

        // The route the dashboard builds from the id, and the only one that works on local disk.
        var content = await client.GetAsync($"/api/media/{fileId}/content");
        await content.EnsureOkAsync("the content route");

        Assert.Equal("image/png", content.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png.OnePixel.Length, (await content.Content.ReadAsByteArrayAsync()).Length);

        await client.SendJsonAsync(HttpMethod.Delete, $"/api/products/{productId}");
    }

    /// <summary>
    /// Every language in one write, which is what makes an editor saving three at once unable to
    /// half-succeed — and a null still removes its row rather than storing a null.
    /// </summary>
    [RequiresDockerFact]
    public async Task Alt_text_for_several_languages_is_written_as_one_batch()
    {
        var client = app.Client;

        var upload = await client.UploadAsync("/api/media", "batch.png");
        var fileId = upload.Str("id");

        await client.SendJsonOkAsync(HttpMethod.Put, $"/api/translations/MediaFile/{fileId}", new
        {
            translations = new Dictionary<string, object>
            {
                ["en"] = new Dictionary<string, string?> { ["AltText"] = "English alt", ["Caption"] = "English caption" },
                ["ar"] = new Dictionary<string, string?> { ["AltText"] = "نص بديل", ["Caption"] = "تعليق" },
                ["fr"] = new Dictionary<string, string?> { ["AltText"] = "Texte alternatif", ["Caption"] = "Légende" }
            }
        });

        var stored = (await client.GetJsonAsync($"/api/translations/MediaFile/{fileId}"))
            .GetProperty("translations");

        Assert.Equal("English alt", stored.GetProperty("en").Str("AltText"));
        Assert.Equal("نص بديل", stored.GetProperty("ar").Str("AltText"));
        Assert.Equal("Légende", stored.GetProperty("fr").Str("Caption"));

        // A null removes the row rather than storing a null — the store's contract, and it has to
        // survive batching alongside a value that is being written in the same call.
        await client.SendJsonOkAsync(HttpMethod.Put, $"/api/translations/MediaFile/{fileId}",
            new { translations = new Dictionary<string, object>
            {
                ["fr"] = new Dictionary<string, string?> { ["AltText"] = null, ["Caption"] = "Légende seule" }
            } });

        var afterNull = (await client.GetJsonAsync($"/api/translations/MediaFile/{fileId}"))
            .GetProperty("translations").GetProperty("fr");

        Assert.DoesNotContain("AltText", afterNull.Keys());
        Assert.Equal("Légende seule", afterNull.Str("Caption"));
    }

    /// <summary>
    /// Deleting a file now takes its alt text with it, through DotNetBoost.Media's OWN endpoint —
    /// the path that used to strand those rows, because only the dashboard knew to follow up and a
    /// direct API caller never did. <c>MediaTextCleanup</c> is registered as an
    /// <c>IMediaFilesDeletedHandler</c>, so the cascade happens for every delete whatever triggered
    /// it, and no call site has to remember it.
    /// <para>
    /// MultiLingual's own translation delete stays idempotent anyway: a subject is addressed by two
    /// strings and the library never resolves them to a row, so deleting the translations of a file
    /// that no longer exists is an ordinary call answering zero, not a 404. Asserted below, because
    /// that is what let the cascade be the only caller without anything else breaking.
    /// </para>
    /// </summary>
    [RequiresDockerFact]
    public async Task Deleting_a_file_takes_its_alt_text_with_it()
    {
        var client = app.Client;

        var upload = await client.UploadAsync("/api/media", "doomed.png");
        var fileId = Guid.Parse(upload.Str("id"));

        await client.SendJsonOkAsync(HttpMethod.Put, $"/api/translations/MediaFile/{fileId}",
            new { translations = new Dictionary<string, object>
            {
                ["en"] = new Dictionary<string, string?> { ["AltText"] = "Briefly here" }
            } });

        await app.WithDbAsync(async db =>
            Assert.True(await CountAsync(db, fileId) > 0, "the text should have been stored"));

        // DotNetBoost.Media's own delete, with nothing in this application telling it about
        // translations. The handler is what connects the two.
        var fileDeleted = await client.SendJsonAsync(HttpMethod.Delete, $"/api/media/{fileId}");
        await fileDeleted.EnsureOkAsync("the media delete");

        await app.WithDbAsync(async db =>
            Assert.Equal(0, await CountAsync(db, fileId)));

        // And deleting the translations of a file that is already gone is an ordinary call
        // answering zero, rather than a 404 about a subject the library never resolves.
        var textDeleted = await client.SendJsonAsync(
            HttpMethod.Delete, $"/api/translations/MediaFile/{fileId}");
        var body = await textDeleted.ReadJsonAsync("the translation delete");

        Assert.Equal(0, body.Int("deletedTranslations"));
        Assert.Equal(MediaText.Subject, body.Str("entityName"));
    }

    private static Task<int> CountAsync(AppDbContext db, Guid fileId)
    {
        var key = fileId.ToString("D");

        return Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
            db.Set<DotNetBoost.MultiLingual.Core.Models.Translation>()
                .Where(t => t.EntityName == "MediaFile" && t.EntityId == key));
    }
}

/// <summary>
/// <inheritdoc cref="MediaShapeTests" path="/summary/node()[1]"/>
/// <para>Run in the mode the sample ships in: every language is a translation row.</para>
/// </summary>
[Collection(SampleAppCollection.Name)]
public sealed class MediaShapeTestsInDefaultMode(SampleApp app) : MediaShapeTests(app);

/// <summary>
/// The same assertions about the shapes media travels in, against the same application booted with
/// <c>MultiLingualOptions.BaseLanguage = "en"</c>.
///
/// <para>Running them twice rather than reasoning about which ones could care is the point: the
/// mode moves content between a column and a translation row, and the claim this sample makes is
/// that no caller can tell. A claim like that is worth more re-run than re-read.</para>
/// </summary>
[Collection(BaseLanguageSampleAppCollection.Name)]
public sealed class MediaShapeTestsInBaseLanguageMode(BaseLanguageSampleApp app) : MediaShapeTests(app);
