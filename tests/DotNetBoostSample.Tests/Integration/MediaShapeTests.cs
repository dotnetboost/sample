using Xunit;

namespace DotNetBoostSample.Tests.Integration;

/// <summary>
/// What the API says about a file, and the distinction it has to keep: a FILE, which a listing is
/// about, and a file IN A ROLE, which an editor is about. The media library draws the same line
/// between <c>ListAsync</c> and <c>ListWithRolesAsync</c>.
/// </summary>
[Collection(SampleAppCollection.Name)]
public class MediaShapeTests(SampleApp app)
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

        await client.SendJsonCheckedAsync(HttpMethod.Post, $"/api/media-text/{fileId}", new
        {
            values = new Dictionary<string, object>
            {
                ["en"] = new { altText = "English alt", caption = "English caption" },
                ["ar"] = new { altText = "نص بديل", caption = "تعليق" },
                ["fr"] = new { altText = "Texte alternatif", caption = "Légende" }
            }
        });

        var stored = (await client.GetJsonAsync($"/api/media-text/{fileId}")).GetProperty("byLanguage");

        Assert.Equal("English alt", stored.GetProperty("en").Str("AltText"));
        Assert.Equal("نص بديل", stored.GetProperty("ar").Str("AltText"));
        Assert.Equal("Légende", stored.GetProperty("fr").Str("Caption"));

        // A null removes the row rather than storing a null — the store's contract, and it has to
        // survive batching alongside a value that is being written in the same call.
        await client.SendJsonCheckedAsync(HttpMethod.Post, $"/api/media-text/{fileId}",
            new { values = new Dictionary<string, object> { ["fr"] = new { altText = (string?)null, caption = "Légende seule" } } });

        var afterNull = (await client.GetJsonAsync($"/api/media-text/{fileId}"))
            .GetProperty("byLanguage").GetProperty("fr");

        Assert.DoesNotContain("AltText", afterNull.Keys());
        Assert.Equal("Légende seule", afterNull.Str("Caption"));
    }

    /// <summary>
    /// The media-text delete is cleanup AFTER a file is gone, so it must not require one to exist —
    /// the dashboard deletes through the media library's own endpoint first, and this runs second.
    /// Adding the existence check its neighbours have would strand exactly the rows it removes.
    /// </summary>
    [RequiresDockerFact]
    public async Task The_text_delete_still_works_once_the_file_itself_is_gone()
    {
        var client = app.Client;

        var upload = await client.UploadAsync("/api/media", "doomed.png");
        var fileId = Guid.Parse(upload.Str("id"));

        await client.SendJsonCheckedAsync(HttpMethod.Post, $"/api/media-text/{fileId}",
            new { values = new Dictionary<string, object> { ["en"] = new { altText = "Briefly here", caption = (string?)null } } });

        // The dashboard's order: the library's own delete first.
        var fileDeleted = await client.SendJsonAsync(HttpMethod.Delete, $"/api/media/{fileId}");
        await fileDeleted.EnsureOkAsync("the media delete");

        // Nothing cascaded, so the rows are still there.
        await app.WithDbAsync(async db =>
            Assert.True(await CountAsync(db, fileId) > 0, "the media delete should not have touched the translations"));

        // And the cleanup call succeeds against a file that no longer exists.
        var textDeleted = await client.SendJsonAsync(HttpMethod.Delete, $"/api/media-text/{fileId}", language: "fr");
        var body = await textDeleted.ReadJsonAsync("the text delete");

        Assert.Equal(1, body.Int("removed"));
        Assert.Contains(fileId.ToString(), body.Str("message"));
        Assert.StartsWith("Le texte du fichier média", body.Str("message"));

        await app.WithDbAsync(async db => Assert.Equal(0, await CountAsync(db, fileId)));

        // Calling it again is ordinary rather than an error: nothing left to remove.
        var again = await (await client.SendJsonAsync(HttpMethod.Delete, $"/api/media-text/{fileId}"))
            .ReadJsonAsync("the second text delete");

        Assert.Equal(0, again.Int("removed"));
    }

    private static Task<int> CountAsync(Api.Data.AppDbContext db, Guid fileId)
    {
        var key = fileId.ToString("D");

        return Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
            db.Set<DotNetBoost.MultiLingual.Core.Models.Translation>()
                .Where(t => t.EntityName == "MediaFile" && t.EntityId == key));
    }
}
