using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.Settings.Core.Interfaces;
using DotNetBoostSample.Api.Integration;
using DotNetBoostSample.Api.Settings;

namespace DotNetBoostSample.Api.Endpoints;

/// <summary>
/// Settings and Media composed with no mechanism whatsoever: a setting holds a media file's id,
/// and this endpoint resolves it. Two libraries, one line of glue, no shared abstraction.
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
            var branding = await settings.For<BrandingSettings>().GetAsync(cancellationToken: ct);

            // The whole integration: a Guid that came out of the settings library goes into the
            // media library.
            var logo = branding.LogoFileId == Guid.Empty
                ? null
                : await media.FindAsync(branding.LogoFileId, ct);

            // And its alt text out of the third library — a logo needs one as much as any other
            // image does.
            var text = logo is null ? null : await mediaText.GetAsync(logo.Id, ct);

            return Results.Ok(new
            {
                branding.StoreName,
                branding.AccentColor,
                branding.LogoFileId,
                // Shaped like an attached file so the dashboard can draw it with the same box it
                // draws a category banner with — though nothing is attached here. The logo belongs
                // to the application, not to a row, so the setting holds the id and the media
                // library has no idea it is being used. "logo" is the box's label, not a media role.
                Logo = logo is null ? null : new
                {
                    logo.Id,
                    logo.FileName,
                    logo.ContentType,
                    logo.Size,
                    Role      = "logo",
                    SortOrder = 0,
                    AltText   = text?.AltText,
                    Caption   = text?.Caption,
                    Url       = $"/api/media/{logo.Id}/content"
                }
            });
        })
        .WithSummary("Branding settings, with the logo file resolved");

// A dedicated upload endpoint used to live here. It is gone: the picker uploads through the
        // media library's own POST /api/media, which already streams, validates and records the
        // file — and then the setting records nothing but the id it comes back with. One fewer
        // endpoint for this application to own.
    }
}
