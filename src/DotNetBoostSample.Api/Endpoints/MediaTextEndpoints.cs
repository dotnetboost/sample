using DotNetBoost.Media.Core.Interfaces;
using DotNetBoostSample.Api.Integration;
using DotNetBoostSample.Api.Localization;
using Microsoft.Extensions.Localization;

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
    /// <summary>A 404 naming the file, in the caller's language. See CatalogEndpoints for the shape.</summary>
    private static IResult FileNotFound(IStringLocalizer<ApiMessages> messages, Guid mediaFileId) =>
        Results.Problem(
            detail: messages["MediaText.FileNotFound", mediaFileId],
            statusCode: StatusCodes.Status404NotFound);

    public static void MapMediaTextEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/media-text").WithTags("Media text (composition)");

        group.MapGet("/{mediaFileId:guid}", async (
            Guid mediaFileId,
            IMediaManager media,
            MediaText mediaText,
            IStringLocalizer<ApiMessages> messages,
            CancellationToken ct) =>
        {
            // Proves the file exists in the media library before answering about its text.
            var file = await media.FindAsync(mediaFileId, ct);
            if (file is null) return FileNotFound(messages, mediaFileId);

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
            IStringLocalizer<ApiMessages> messages,
            CancellationToken ct) =>
        {
            var file = await media.FindAsync(mediaFileId, ct);
            if (file is null) return FileNotFound(messages, mediaFileId);

            foreach (var (language, values) in body.Values)
            {
                await mediaText.SetAsync(mediaFileId, language, MediaText.AltText, values.AltText, ct);
                await mediaText.SetAsync(mediaFileId, language, MediaText.Caption, values.Caption, ct);
            }

            return Results.Ok(await mediaText.GetAllLanguagesAsync(mediaFileId, ct));
        })
        .WithSummary("Sets alt text and caption for one or more languages");

        group.MapPost("/resolve", async (
            ResolveMediaText body,
            MediaText mediaText,
            CancellationToken ct) =>
        {
            // One query for every file, not one per file — which is why MediaText exposes
            // GetManyAsync at all. The media library's own list endpoint cannot do this: it has
            // no alt-text field, and no idea the translation rows exist.
            var resolved = await mediaText.GetManyAsync(body.Ids, ct);

            return Results.Ok(resolved.ToDictionary(pair => pair.Key, pair => pair.Value));
        })
        .WithSummary("Alt text and caption for many files at once, in the request's language");

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

/// <summary>The files to resolve text for, in the request's language.</summary>
public sealed record ResolveMediaText(Guid[] Ids);
