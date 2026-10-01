using DotNetBoost.Media.Core.Interfaces;
using DotNetBoostSample.Api.Integration;

namespace DotNetBoostSample.Api.Endpoints;

/// <summary>
/// The composition, exposed as its own endpoints so the dashboard can edit alt text per language.
/// <para>
/// Worth noting that these endpoints exist at all: DotNetBoost.MultiLingual.API ships
/// <c>MapLanguageEndpoints</c> but no translation-editing endpoints, so writing translations over
/// HTTP is the application's job. That is a real gap this sample surfaced.
/// </para>
/// </summary>
public static class MediaTextEndpoints
{
    public static void MapMediaTextEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/media-text").WithTags("Media text (composition)");

        group.MapGet("/{mediaFileId:guid}", async (
            Guid mediaFileId,
            IMediaManager media,
            MediaText mediaText,
            CancellationToken ct) =>
        {
            // Proves the file exists in the media library before answering about its text.
            var file = await media.FindAsync(mediaFileId, ct);
            if (file is null) return Results.NotFound();

            var current = await mediaText.GetAsync(mediaFileId, ct);
            var all     = await mediaText.GetAllLanguagesAsync(mediaFileId, ct);

            return Results.Ok(new
            {
                MediaFileId = mediaFileId,
                file.FileName,
                Current = current,
                ByLanguage = all
                    .GroupBy(t => t.LanguageCode)
                    .ToDictionary(
                        g => g.Key,
                        g => g.ToDictionary(t => t.PropertyName, t => t.Value))
            });
        })
        .WithSummary("Alt text and caption for a file: resolved, and every stored language");

        group.MapPost("/{mediaFileId:guid}", async (
            Guid mediaFileId,
            MediaTextWrite body,
            IMediaManager media,
            MediaText mediaText,
            CancellationToken ct) =>
        {
            var file = await media.FindAsync(mediaFileId, ct);
            if (file is null) return Results.NotFound();

            foreach (var (language, values) in body.Values)
            {
                await mediaText.SetAsync(mediaFileId, language, MediaText.AltText, values.AltText, ct);
                await mediaText.SetAsync(mediaFileId, language, MediaText.Caption, values.Caption, ct);
            }

            return Results.Ok(await mediaText.GetAllLanguagesAsync(mediaFileId, ct));
        })
        .WithSummary("Sets alt text and caption for one or more languages");

        group.MapDelete("/{mediaFileId:guid}", async (
            Guid mediaFileId,
            MediaText mediaText,
            CancellationToken ct) =>
        {
            await mediaText.DeleteAsync(mediaFileId, ct);
            return Results.NoContent();
        })
        .WithSummary("Removes every language's text for a file");
    }
}

/// <summary>Alt text and caption, keyed by language code.</summary>
public sealed record MediaTextWrite(Dictionary<string, MediaTextValues> Values);
