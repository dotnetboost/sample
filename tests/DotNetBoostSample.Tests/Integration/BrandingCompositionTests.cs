using System.Net;
using Xunit;

namespace DotNetBoostSample.Tests.Integration;

/// <summary>
/// Branding composes all three libraries on one subject that has no table: its text is
/// DotNetBoost.Settings, its translated name is DotNetBoost.MultiLingual, and its logo is a
/// DotNetBoost.Media attachment on the owner <c>("Branding", "branding")</c>.
/// </summary>
[Collection(SampleAppCollection.Name)]
public class BrandingCompositionTests(SampleApp app)
{
    /// <summary>
    /// The regression test for 9d3f782, and the reason this whole fixture exists.
    ///
    /// <para><c>BrandingEndpoints</c> used to resolve the translated store name by assigning to the
    /// instance <c>SettingManager</c> handed it — which is the CACHED instance, not a copy. Reading
    /// branding in Arabic therefore wrote Arabic into the entry the group is served from, and
    /// <c>GET /api/settings/branding</c> would hand that Arabic back to the next reader as though it
    /// were the stored setting.</para>
    ///
    /// <para>Nothing in the reflection tests could see this, and in production it hid behind the
    /// language-keyed cache. It is exactly the shape of bug an integration test is for: two
    /// endpoints, one cache, and a defect that only appears in the second call.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task Reading_branding_in_one_language_does_not_change_the_settings_row()
    {
        var client = app.Client;

        var stored = (await client.GetJsonAsync("/api/settings/branding")).Str("storeName");

        // Give the group an Arabic name, which is a translation row rather than a setting value.
        // Through MultiLingual's own translation endpoint, addressing the settings group as the
        // subject ("Branding", "branding") its [MultiLingualEntity] and Id declare.
        await client.SendJsonOkAsync(HttpMethod.Put, "/api/translations/Branding/branding",
            new { translations = new Dictionary<string, object>
            {
                ["ar"] = new Dictionary<string, string?> { ["StoreName"] = "متجر العينة" }
            } });

        // Reading branding in Arabic is what used to poison the cache.
        var arabic = await client.GetJsonAsync("/api/branding", "ar");
        Assert.Equal("متجر العينة", arabic.Str("storeName"));

        // The settings group must still answer with the stored value, in any language. Before the
        // fix this came back as "متجر العينة".
        Assert.Equal(stored, (await client.GetJsonAsync("/api/settings/branding")).Str("storeName"));
        Assert.Equal(stored, (await client.GetJsonAsync("/api/settings/branding", "ar")).Str("storeName"));

        // And branding itself still falls back to the setting where no translation exists.
        Assert.Equal(stored, (await client.GetJsonAsync("/api/branding", "fr")).Str("storeName"));
    }

    /// <summary>
    /// The sample's sharpest claim about the attachment model: a thing with no row anywhere owns a
    /// file, because an owner is only ever two strings the media library never resolves.
    /// </summary>
    [RequiresDockerFact]
    public async Task A_settings_group_with_no_table_can_own_a_file()
    {
        var client = app.Client;

        var file = await client.UploadAsync("/api/media", "logo.png");
        var fileId = file.Str("id");

        await client.SendJsonCheckedAsync(HttpMethod.Post, $"/api/media/{fileId}/attachments", new
        {
            ownerEntityName = "Branding",
            ownerEntityId = "branding",
            role = "logo",
            replace = true
        });

        var branding = await client.GetJsonAsync("/api/branding");

        Assert.Equal(fileId, branding.GetProperty("logo").Str("id"));

        // And it comes back in the same role-slot shape a product's images do, which is what lets
        // the dashboard draw it with the same component.
        var images = branding.GetProperty("images").EnumerateArray().ToList();
        Assert.Contains(images, image => image.Str("role") == "logo" && image.Str("id") == fileId);
    }

    /// <summary>
    /// The settings group's conditional save. The <c>ETag</c> from the read has to be accepted as
    /// <c>If-Match</c>, and a stale one has to be refused — which is what stops two editors
    /// silently overwriting each other.
    /// </summary>
    [RequiresDockerFact]
    public async Task A_save_against_a_stale_revision_is_refused()
    {
        var client = app.Client;

        var read = await client.GetAsync("/api/settings/branding");
        var etag = read.Headers.ETag?.ToString();
        Assert.False(string.IsNullOrEmpty(etag), "GET /api/settings/branding sent no ETag to save against.");

        var current = await read.ReadJsonAsync("GET /api/settings/branding");

        object Group(int size) => new
        {
            storeName = current.Str("storeName"),
            accentColor = current.Str("accentColor"),
            headerLogoSize = size
        };

        // Somebody else saves first, using the revision we also hold.
        var theirs = await client.SendJsonAsync(HttpMethod.Post, "/api/settings/branding", Group(31), ifMatch: etag);
        await theirs.EnsureOkAsync("the first save");

        // Ours is now based on a revision that has moved on.
        var ours = await client.SendJsonAsync(HttpMethod.Post, "/api/settings/branding", Group(47), ifMatch: etag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, ours.StatusCode);

        // Theirs stands, untouched by the refused write.
        Assert.Equal(31, (await client.GetJsonAsync("/api/settings/branding")).Int("headerLogoSize"));
    }
}
