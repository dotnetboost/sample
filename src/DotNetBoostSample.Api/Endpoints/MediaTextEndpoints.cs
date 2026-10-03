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

            // Every language in one write. A loop here would be several, and an editor that saves
            // three languages at once should not be able to half-succeed.
            await mediaText.SetManyAsync(mediaFileId, body.Values ?? [], ct);

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
            IStringLocalizer<ApiMessages> messages,
            CancellationToken ct) =>
        {
            // ── The one handler here that does NOT check the file exists ─────
            //
            // The GET and POST above call media.FindAsync first, because they are about a file
            // someone is looking at. This one is cleanup AFTER a file is gone: the Media page
            // deletes through DotNetBoost.Media's own endpoint, which has never heard of these
            // rows, and then calls this. By the time the request arrives the file is already
            // deleted — so a 404 here would reject the only caller there is and strand exactly
            // the rows this endpoint exists to remove. The asymmetry is the point, not an
            // oversight.
            var removed = await mediaText.DeleteAsync(mediaFileId, ct);

            // 200 and a sentence rather than 204, like every other write in this API: a 204 is
            // defined to have no body, so there is nowhere to put one. `removed` is the count,
            // and zero is an ordinary answer — a file nobody ever wrote alt text for.
            //
            // The message names the file by id rather than by name, for the same reason the 404s
            // do: by the time this runs there may be no file left to read a name from, and the id
            // is the only handle certain to still exist.
            return Results.Ok(new
            {
                Removed = removed,
                Message = messages["MediaText.Deleted", mediaFileId].Value
            });
        })
        .WithSummary("Removes every language's text for a file, whether or not the file is still there");
    }
}

/// <summary>
/// Alt text and caption, keyed by language code.
/// <para>
/// Nullable because the wire can leave it out: a body of <c>{}</c> deserialises to a null here
/// whatever the annotation says, and the handler treats that as "no languages supplied" rather
/// than answering 500.
/// </para>
/// </summary>
public sealed record MediaTextWrite(Dictionary<string, MediaTextValues>? Values);

/// <summary>The files to resolve text for, in the request's language.</summary>
public sealed record ResolveMediaText(Guid[] Ids);
