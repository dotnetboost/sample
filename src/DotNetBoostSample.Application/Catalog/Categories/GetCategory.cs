using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoostSample.Application.Abstractions.Endpoints;
using DotNetBoostSample.Application.Abstractions.Cqrs;
using DotNetBoostSample.Application.Abstractions.Persistence;
using DotNetBoostSample.Application.Abstractions.Results;
using DotNetBoostSample.Application.Catalog.Shared;
using DotNetBoostSample.Application.Composition;
using DotNetBoostSample.Application.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace DotNetBoostSample.Application.Catalog.Categories;

/// <summary>
/// One category with every language's text and its banner — the shape the dashboard's editor reads.
///
/// <para>This is the exception to "the interceptors already translated it": an editor needs every
/// language at once, which is what <c>TranslationReads.LoadAsync</c> answers with — the
/// stored translations, plus the base language's own columns where a base language is
/// configured.</para>
/// </summary>
public static class GetCategory
{
    public sealed record Query(int Id) : IQuery<Result<CategoryDetail>>;

    /// <param name="Translations">languageCode → property → value, for every language.</param>
    /// <param name="Images">Every attached file WITH the role it is attached under, which is what
    /// the editor's role boxes are drawn from. The roles themselves come from the media library's
    /// own GET /api/media/roles/Category — declared at startup, not here.</param>
    public sealed record CategoryDetail(
        int Id,
        string Slug,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> Translations,
        IReadOnlyList<AttachedFileInRole> Images);

    public sealed class Handler(
        IAppDbContext db,
        IMediaManager media,
        IMultiLingualManager multiLingual,
        MediaText mediaText) : IRequestHandler<Query, Result<CategoryDetail>>
    {
        /// <inheritdoc/>
        public async Task<Result<CategoryDetail>> HandleAsync(Query request, CancellationToken ct)
        {
            var (category, text) = await TranslationReads.LoadAsync(
                multiLingual, db.Categories.Where(c => c.Id == request.Id), ct);

            if (category is null) return Error.NotFound("Catalog.CategoryNotFound", request.Id);

            return new CategoryDetail(
                category.Id,
                category.Slug,
                text,
                await AttachedFiles.DescribeAsync(media.For(category), mediaText, ct));
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        /// <inheritdoc/>
        public void Map(IEndpointRouteBuilder app) =>
            app.MapGet("/api/categories/{id:int}", async (
                    int id,
                    IDispatcher dispatcher,
                    IStringLocalizer<ApiMessages> messages,
                    CancellationToken ct) =>
                (await dispatcher.SendAsync(new Query(id), ct)).ToHttpResult(messages))
               .WithTags("Categories")
               // Declared, because an endpoint answering 200-or-404 has to return IResult and the
               // document cannot infer a thing from that.
               .Produces<CategoryDetail>(StatusCodes.Status200OK)
               .ProducesProblem(StatusCodes.Status404NotFound)
               .WithSummary("One category with every language's text and its banner");
    }
}
