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
            // language's text INTO the entry every later reader of the group is served from — and
            // `GET /api/settings/branding` would hand that back as though it were the stored value.
            //
            // Projecting into the response is the only thing preventing that: there is no
            // language-keyed cache behind it to soften the blow, and nothing else would notice.
            // tests/.../Integration/BrandingCompositionTests.cs is what holds it.
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

    }

    private static async Task<object?> LogoAsync(
        IMediaAccessor owner,
        MediaText mediaText,
        CancellationToken ct)
    {
        var logo = await owner.GetByRoleAsync(BrandingSettings.LogoRole, ct);

        return logo is null
            ? null
            : AttachedFiles.Describe(logo, await mediaText.GetAsync(logo.Id, ct));
    }
}
