using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.Settings.Core.Interfaces;
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
            CancellationToken ct) =>
        {
            var branding = await settings.For<BrandingSettings>().GetAsync(cancellationToken: ct);

            // The whole integration: a Guid that came out of the settings library goes into the
            // media library.
            var logo = branding.LogoFileId == Guid.Empty
                ? null
                : await media.FindAsync(branding.LogoFileId, ct);

            return Results.Ok(new
            {
                branding.StoreName,
                branding.AccentColor,
                branding.LogoFileId,
                Logo = logo is null ? null : new
                {
                    logo.Id,
                    logo.FileName,
                    logo.ContentType,
                    logo.Size,
                    Url = $"/api/media/{logo.Id}/content"
                }
            });
        })
        .WithSummary("Branding settings, with the logo file resolved");

        group.MapPost("/logo", async (
            IFormFile file,
            ISettingManager settings,
            IMediaManager media,
            CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();

            // Uploaded with no owner entity: the logo belongs to the application, not to a row.
            var uploaded = await media.UploadAsync(
                new DotNetBoost.Media.Core.Models.MediaUpload
                {
                    Content     = stream,
                    FileName    = file.FileName,
                    ContentType = file.ContentType,
                    Category    = "branding"
                },
                ct);

            var accessor = settings.For<BrandingSettings>();
            var branding = await accessor.GetAsync(cancellationToken: ct);

            branding.LogoFileId = uploaded.Id;
            await accessor.SetAsync(branding, ct);

            return Results.Ok(new { branding.LogoFileId, uploaded.FileName });
        })
        .DisableAntiforgery()
        .WithSummary("Uploads a logo and stores its id in the branding settings");
    }
}
