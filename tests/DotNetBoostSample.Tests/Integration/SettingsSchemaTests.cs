using System.Net;
using System.Reflection;
using System.Net.Http;
using Xunit;

namespace DotNetBoostSample.Tests.Integration;

/// <summary>
/// The settings groups describe themselves, and the rules they publish are the rules they enforce.
/// <para>
/// This is what let the dashboard stop guessing. <c>clients/dashboard/app/utils/settings.ts</c>
/// hardcoded <c>headerLogoSize: { min: 16, max: 64 }</c> and <c>pageSize: { min: 1, max: 200 }</c> —
/// two ranges that appeared nowhere in C# and that the API would have accepted anything outside.
/// They come from each group's own file under <c>Settings/</c> now, through
/// <c>DotNetBoost.Settings.FluentValidation</c>'s schema contributor.
/// </para>
/// </summary>
public abstract class SettingsSchemaTests(SampleApp app)
{
    [RequiresDockerFact]
    public async Task The_group_list_names_every_registered_group_and_no_values()
    {
        var groups = await app.Client.GetJsonAsync("/api/settings");

        var routes = groups.EnumerateArray().Select(g => g.Str("route")).Order().ToList();

        Assert.Equal(["branding", "catalog"], routes);

        // Routes and names, never values: this endpoint is meant to be safe to expose more widely
        // than the groups themselves.
        foreach (var group in groups.EnumerateArray())
        {
            Assert.Equal(["name", "requiresAuthorization", "route"], group.Keys().Order());
        }
    }

    [RequiresDockerFact]
    public async Task The_schema_publishes_the_bounds_the_validators_enforce()
    {
        var branding = await app.Client.GetJsonAsync("/api/settings/branding/schema");
        var logoSize = Property(branding, "headerLogoSize");

        Assert.Equal(16, logoSize.GetProperty("constraints").Int("min"));
        Assert.Equal(64, logoSize.GetProperty("constraints").Int("max"));
        Assert.Equal("Int32", logoSize.Str("type"));
        Assert.Equal(28, logoSize.GetProperty("default").GetInt32());

        var catalog = await app.Client.GetJsonAsync("/api/settings/catalog/schema");
        var pageSize = Property(catalog, "pageSize");

        // The upper bound is CatalogRoles.MaxPageSize: a stored page size above the cap would
        // be clamped on every read, so the validator refuses it rather than storing a lie.
        Assert.Equal(1, pageSize.GetProperty("constraints").Int("min"));
        Assert.Equal(200, pageSize.GetProperty("constraints").Int("max"));
    }

    [RequiresDockerFact]
    public async Task The_schema_reports_no_values_only_their_shape()
    {
        var branding = await app.Client.GetJsonAsync("/api/settings/branding/schema");

        foreach (var property in branding.GetProperty("properties").EnumerateArray())
        {
            Assert.Equal(
                ["constraints", "default", "name", "nullable", "sensitive", "type"],
                property.Keys().Order());

            Assert.False(property.GetProperty("sensitive").GetBoolean());
        }
    }

    [RequiresDockerFact]
    public async Task A_write_outside_the_published_bounds_is_refused()
    {
        try
        {
            var refused = await app.Client.SendJsonAsync(HttpMethod.Post, "/api/settings/catalog", new
            {
                pageSize = 5000,               // above MaxPageSize, so above the published max
                currencySymbol = "$"
            });

            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

            // And nothing was stored: the group still answers with what it had.
            Assert.Equal(12, (await app.Client.GetJsonAsync("/api/settings/catalog")).Int("pageSize"));
        }
        finally
        {
            await app.ResetCatalogSettingsAsync();
        }
    }

    [RequiresDockerFact]
    public async Task A_value_inside_the_published_bounds_is_accepted()
    {
        try
        {
            await app.Client.PostAsJsonChecked("/api/settings/catalog", new
            {
                pageSize = 200,                // exactly the published max
                currencySymbol = "€"
            });

            var stored = await app.Client.GetJsonAsync("/api/settings/catalog");

            Assert.Equal(200, stored.Int("pageSize"));
            Assert.Equal("€", stored.Str("currencySymbol"));
        }
        finally
        {
            await app.ResetCatalogSettingsAsync();
        }
    }

    /// <summary>
    /// The two contributors compose. DotNetBoost.Settings.FluentValidation describes the rules in
    /// <c>BrandingSettingsValidator</c>; this application's <c>MultiLingualSchemaContributor</c>
    /// describes <c>[MultiLingual]</c>, an attribute the settings library cannot know about because
    /// it belongs to DotNetBoost.MultiLingual. Before preview.2 the schema endpoint took only the
    /// first contributor, so registering the second would have deleted the first's constraints.
    /// </summary>
    [RequiresDockerFact]
    public async Task A_translated_property_carries_its_validation_rules_as_well_as_the_flag()
    {
        var branding = await app.Client.GetJsonAsync("/api/settings/branding/schema");
        var constraints = Property(branding, "storeName").GetProperty("constraints");

        // From MultiLingualSchemaContributor.
        Assert.True(constraints.GetProperty("translated").GetBoolean());

        // And from FluentValidation, in the same object rather than instead of it.
        Assert.True(constraints.GetProperty("required").GetBoolean());
        Assert.Equal(120, constraints.Int("maxLength"));
    }

    /// <summary>
    /// The schema flags exactly the properties the C# class marks `[MultiLingual]` — reflected over
    /// rather than listed, so adding or removing one is covered without touching this test.
    /// <para>
    /// Worth asserting as a set rather than on one known property: the dashboard's per-language
    /// editor builds its boxes from this flag, and it used to hardcode a single name. Adding a
    /// second translatable property then changed the schema correctly and the editor not at all.
    /// </para>
    /// </summary>
    [RequiresDockerFact]
    public async Task Every_MultiLingual_property_is_flagged_translated_and_no_others()
    {
        var expected = typeof(DotNetBoostSample.Application.Settings.BrandingSettings)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<DotNetBoost.MultiLingual.Core.Attributes.MultiLingualAttribute>() is not null)
            .Select(p => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(p.Name))
            .Order()
            .ToList();

        Assert.NotEmpty(expected);   // otherwise this would pass by describing nothing

        var branding = await app.Client.GetJsonAsync("/api/settings/branding/schema");

        var flagged = branding.GetProperty("properties").EnumerateArray()
            .Where(p => p.GetProperty("constraints").TryGetProperty("translated", out var t) && t.GetBoolean())
            .Select(p => p.Str("name"))
            .Order()
            .ToList();

        Assert.Equal(expected, flagged);
    }

    [RequiresDockerFact]
    public async Task A_property_with_no_MultiLingual_attribute_is_not_flagged()
    {
        var branding = await app.Client.GetJsonAsync("/api/settings/branding/schema");

        // accentColor has validation rules but no [MultiLingual]: the flag must be absent, not
        // false, so a client can tell "not translated" from "nobody said".
        Assert.DoesNotContain("translated", Property(branding, "accentColor").GetProperty("constraints").Keys());

        // And a group with no translatable property at all is described by one contributor only.
        var catalog = await app.Client.GetJsonAsync("/api/settings/catalog/schema");

        foreach (var property in catalog.GetProperty("properties").EnumerateArray())
            Assert.DoesNotContain("translated", property.GetProperty("constraints").Keys());
    }

    private static System.Text.Json.JsonElement Property(System.Text.Json.JsonElement schema, string name) =>
        schema.GetProperty("properties").EnumerateArray().Single(p => p.Str("name") == name);
}

/// <summary>
/// <inheritdoc cref="SettingsSchemaTests" path="/summary/node()[1]"/>
/// <para>Run in the mode the sample ships in: every language is a translation row.</para>
/// </summary>
[Collection(SampleAppCollection.Name)]
public sealed class SettingsSchemaTestsInDefaultMode(SampleApp app) : SettingsSchemaTests(app);

/// <summary>
/// The same assertions about the published settings schema, against the same application booted with
/// <c>MultiLingualOptions.BaseLanguage = "en"</c>.
///
/// <para>Running them twice rather than reasoning about which ones could care is the point: the
/// mode moves content between a column and a translation row, and the claim this sample makes is
/// that no caller can tell. A claim like that is worth more re-run than re-read.</para>
/// </summary>
[Collection(BaseLanguageSampleAppCollection.Name)]
public sealed class SettingsSchemaTestsInBaseLanguageMode(BaseLanguageSampleApp app) : SettingsSchemaTests(app);
