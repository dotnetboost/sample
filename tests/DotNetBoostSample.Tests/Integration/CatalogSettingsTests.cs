using Xunit;

namespace DotNetBoostSample.Tests.Integration;

/// <summary>
/// The catalogue settings group decides what <c>GET /api/products</c> answers with — the sample's
/// one demonstration that a settings library is worth having, because the value changes the API
/// without a restart.
///
/// <para>Every test here puts the group back in a <c>finally</c>. The collection shares one
/// application and one database, and a page size of two left behind would quietly change what the
/// other tests see.</para>
/// </summary>
[Collection(SampleAppCollection.Name)]
public class CatalogSettingsTests(SampleApp app)
{
    /// <summary>
    /// The claim in one test: edit the setting, call the same URL again, get a different page. No
    /// restart, and no page size sent by the caller.
    /// </summary>
    [RequiresDockerFact]
    public async Task PageSize_changes_the_listing_without_a_restart()
    {
        var client = app.Client;

        try
        {
            var before = await client.GetJsonAsync("/api/products");
            Assert.Equal(12, before.Int("take"));

            await client.PostAsJsonChecked("/api/settings/catalog", new
            {
                pageSize = 2,
                currencySymbol = "$",
                showProductsWithoutImages = true
            });

            var after = await client.GetJsonAsync("/api/products");

            Assert.Equal(2, after.Int("take"));
            Assert.Equal(2, after.GetProperty("items").GetArrayLength());

            // The total is the whole filtered set, not the page — which is what lets a pager exist.
            Assert.True(after.Int("total") > 2, "the total should count past the page");

            // And paging actually moves.
            var second = await client.GetJsonAsync("/api/products?page=2");
            Assert.Equal(2, second.Int("skip"));

            var firstIds = after.GetProperty("items").EnumerateArray().Select(p => p.Int("id")).ToList();
            var secondIds = second.GetProperty("items").EnumerateArray().Select(p => p.Int("id")).ToList();
            Assert.Empty(firstIds.Intersect(secondIds));
        }
        finally
        {
            await app.ResetCatalogSettingsAsync();
        }
    }

    /// <summary>
    /// A caller may ask for its own page — that is what the dashboard's overview does — but not for
    /// an unbounded one.
    /// </summary>
    [RequiresDockerFact]
    public async Task A_caller_can_override_the_page_size_but_not_past_the_cap()
    {
        var client = app.Client;

        Assert.Equal(1, (await client.GetJsonAsync("/api/products?pageSize=1")).Int("take"));

        // MaxPageSize in CatalogEndpoints. A page size is a caller's suggestion.
        Assert.Equal(200, (await client.GetJsonAsync("/api/products?pageSize=99999")).Int("take"));
    }

    /// <summary>
    /// <c>ShowProductsWithoutImages</c> crosses a library boundary: "has an image" is a fact about
    /// Media's attachments, asked of every product before a page can be taken. It also has to be
    /// applied BEFORE the count, or the pager offers pages that hold nothing.
    /// </summary>
    [RequiresDockerFact]
    public async Task Products_with_no_image_can_be_left_out_of_the_listing_entirely()
    {
        var client = app.Client;

        try
        {
            // A product nobody has attached anything to.
            var bare = await client.SendJsonCheckedAsync(HttpMethod.Post, "/api/products", new
            {
                sku = $"BARE-{Guid.NewGuid():N}"[..12],
                price = 5.0m,
                categoryId = 1,
                translations = new Dictionary<string, Dictionary<string, string>>
                {
                    ["en"] = new() { ["Name"] = "No picture", ["Description"] = "None at all." }
                }
            });

            var bareId = bare.Int("id");

            // And one with an image.
            var withImage = await client.SendJsonCheckedAsync(HttpMethod.Post, "/api/products", new
            {
                sku = $"PIC-{Guid.NewGuid():N}"[..12],
                price = 5.0m,
                categoryId = 1,
                translations = new Dictionary<string, Dictionary<string, string>>
                {
                    ["en"] = new() { ["Name"] = "Has a picture", ["Description"] = "One of them." }
                }
            });

            var withImageId = withImage.Int("id");
            await client.UploadAsync($"/api/products/{withImageId}/images?role=primary");

            var showingAll = await client.GetJsonAsync("/api/products?pageSize=200");
            Assert.Contains(showingAll.GetProperty("items").EnumerateArray(), p => p.Int("id") == bareId);

            await client.PostAsJsonChecked("/api/settings/catalog", new
            {
                pageSize = 12,
                currencySymbol = "$",
                showProductsWithoutImages = false
            });

            var filtered = await client.GetJsonAsync("/api/products?pageSize=200");
            var ids = filtered.GetProperty("items").EnumerateArray().Select(p => p.Int("id")).ToList();

            Assert.DoesNotContain(bareId, ids);
            Assert.Contains(withImageId, ids);

            // Counted after the filter, so the total matches what is actually listed.
            Assert.Equal(ids.Count, filtered.Int("total"));
            Assert.True(filtered.Int("total") < showingAll.Int("total"));

            await client.SendJsonAsync(HttpMethod.Delete, $"/api/products/{bareId}");
            await client.SendJsonAsync(HttpMethod.Delete, $"/api/products/{withImageId}");
        }
        finally
        {
            await app.ResetCatalogSettingsAsync();
        }
    }

    /// <summary>
    /// The settings form is generated from whatever the group returns, so the group's own property
    /// list is part of its contract with the dashboard — and the branding group deliberately keeps
    /// its addressing <c>Id</c> out of it.
    /// </summary>
    [RequiresDockerFact]
    public async Task A_group_answers_with_its_values_and_nothing_else()
    {
        var client = app.Client;

        Assert.Equal(
            ["currencySymbol", "pageSize", "showProductsWithoutImages"],
            (await client.GetJsonAsync("/api/settings/catalog")).Keys().Order());

        var branding = (await client.GetJsonAsync("/api/settings/branding")).Keys();

        Assert.Contains("storeName", branding);
        Assert.Contains("headerLogoSize", branding);

        // Get-only and [JsonIgnore]: an addressing detail for MultiLingual, not one of the group's
        // values, and the dashboard's generated form would otherwise grow a pointless field for it.
        Assert.DoesNotContain("id", branding);

        // It left when the logo became an attachment, and must not come back: a second record of
        // which file is the logo is one nothing can keep in step.
        Assert.DoesNotContain("logoFileId", branding);
    }
}
