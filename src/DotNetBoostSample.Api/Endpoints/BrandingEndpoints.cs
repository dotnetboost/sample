using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoost.Settings.Core.Interfaces;
using DotNetBoostSample.Api.Integration;
using DotNetBoostSample.Api.Settings;

namespace DotNetBoostSample.Api.Endpoints;

/// <summary>
/// Branding: text from DotNetBoost.Settings, logo from DotNetBoost.Media, composed here and
/// nowhere else.
/// <para>
/// The two halves are read independently and neither library knows about the other. The settings
/// group holds no file id at all — the logo is an attachment on the owner
/// <c>("Branding", "branding")</c>, which has no table behind it. See
/// <see cref="BrandingSettings"/> for why it is modelled that way.
/// </para>
/// </summary>
public static class BrandingEndpoints
{
    public static void MapBrandingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/branding").WithTags("Branding (composition)");

        group.MapGet("/", async (
            ISettingManager settings,
            IMediaManager media,
            IMultiLingualManager multiLingual,
            MediaText mediaText,
            CancellationToken ct) =>
        {
            // Three reads, three libraries, no shared anything.
            var branding = await settings.For<BrandingSettings>().GetAsync(cancellationToken: ct);

            // The store name in the request's language when a translation exists, and the settings
            // row otherwise — that row is the value every other language falls back to.
            //
            // Deliberately NOT LocalizeAsync(): outside base-language mode MultiLingual treats the
            // translations as the ONLY store, so it nulls a property it finds nothing for. Here the
            // settings row IS the base value — the group's own ETag and audit cover it — so a
            // missing translation must leave it alone rather than erase it.
            //
            // And deliberately a LOCAL, not `branding.StoreName = translated`. `GetAsync` hands
            // back the instance SettingManager cached, so assigning to a property of it writes this
            // language's text INTO the entry every later reader of the group is served from. Today
            // Settings/LanguageAwareSettingCache.cs gives each language its own entry, which
            // confines the damage to the one language the text was right for and makes it
            // invisible — which is exactly why it must not be what keeps this correct. Projecting
            // into the response leaves the cached group untouched whatever the cache does.
            var stored = await multiLingual.For(branding).GetAllAsync(ct);

            var storeName =
                stored.TryGetValue(multiLingual.CurrentLanguage.Code, out var inLanguage)
                && inLanguage.TryGetValue(nameof(BrandingSettings.StoreName), out var translated)
                && !string.IsNullOrWhiteSpace(translated)
                    ? translated
                    : branding.StoreName;

            var owner = media.For(BrandingSettings.MediaOwnerName, BrandingSettings.MediaOwnerId);

            return Results.Ok(new
            {
                StoreName = storeName,
                branding.AccentColor,
                branding.HeaderLogoSize,
                // Every attached file with its role — the same shape the catalogue returns, so the
                // dashboard draws branding's media with the very same component it draws a
                // product's with.
                Images = await AttachedFiles.DescribeAsync(owner, mediaText, ct),
                // The logo on its own, for callers that only want to render it.
                Logo = await LogoAsync(owner, mediaText, ct)
            });
        })
        .WithSummary("Branding text in the request's language, and the logo from its attachment");

        // ── The group's text, per language ───────────────────────────────────
        //
        // This is the typed API, not the raw store MediaText had to use. A media file is not an
        // entity, so alt text had to be addressed with literal strings; a settings group IS one as
        // far as MultiLingual is concerned, now that it carries an Id — so the property is named
        // by expression and the entity name comes from [MultiLingualEntity].
        group.MapGet("/text", async (IMultiLingualManager multiLingual, CancellationToken ct) =>
        {
            var all = await multiLingual.For(Subject()).GetAllAsync(ct);

            return Results.Ok(all);
        })
        .WithSummary("Every stored language for the branding group's translatable text");

        group.MapPut("/text", async (
            BrandingTextWrite body,
            IMultiLingualManager multiLingual,
            CancellationToken ct) =>
        {
            // A fresh instance with StoreName left null. SaveAsync writes the entity's translatable
            // values for the CURRENT language as well as anything queued, and a null property means
            // "not supplied" — so nothing is written for the request's own language by accident.
            var accessor = multiLingual.For(Subject());

            foreach (var (language, value) in body.Values)
            {
                accessor.Set(language, b => b.StoreName, value);
            }

            await accessor.SaveAsync(ct);

            return Results.Ok(await multiLingual.For(Subject()).GetAllAsync(ct));
        })
        .WithSummary("Sets the branding group's text in one or more languages");
    }

    /// <summary>
    /// A bare instance to address the group by.
    /// <para>
    /// Its <c>Id</c> is a constant, so this is the same subject every time — no need to load the
    /// group first. <c>StoreName</c> is left null deliberately: to <c>SaveAsync</c> a null property
    /// means "not supplied", which keeps a write for one language from also writing the request's
    /// own.
    /// </para>
    /// </summary>
    private static BrandingSettings Subject() => new() { StoreName = null! };

    private static async Task<object?> LogoAsync(
        IMediaAccessor owner,
        MediaText mediaText,
        CancellationToken ct)
    {
        var logo = await owner.GetByRoleAsync(BrandingSettings.LogoRole, ct);

        return logo is null
            ? null
            : AttachedFiles.Describe(logo, BrandingSettings.LogoRole, 0, await mediaText.GetAsync(logo.Id, ct));
    }
}

/// <summary>The branding group's translatable text, keyed by language code.</summary>
public sealed record BrandingTextWrite(Dictionary<string, string?> Values);
