using DotNetBoost.Media.Core.Interfaces;
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
            MediaText mediaText,
            CancellationToken ct) =>
        {
            // Two reads, two libraries, no shared anything.
            var branding = await settings.For<BrandingSettings>().GetAsync(cancellationToken: ct);

            var owner = media.For(BrandingSettings.MediaOwnerName, BrandingSettings.MediaOwnerId);

            return Results.Ok(new
            {
                branding.StoreName,
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
        .WithSummary("Branding text, and the logo resolved from its attachment");
    }

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
