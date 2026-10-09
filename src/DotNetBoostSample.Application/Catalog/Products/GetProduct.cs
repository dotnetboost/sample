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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace DotNetBoostSample.Application.Catalog.Products;

/// <summary>One product with every language's text and its images, per role.</summary>
public static class GetProduct
{
    public sealed record Query(int Id) : IQuery<Result<ProductDetail>>;

    /// <inheritdoc cref="Categories.GetCategory.CategoryDetail"/>
    public sealed record ProductDetail(
        int Id,
        string Sku,
        decimal Price,
        int CategoryId,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> Translations,
        IReadOnlyList<AttachedFileInRole> Images);

    public sealed class Handler(
        IAppDbContext db,
        IMediaManager media,
        IMultiLingualManager multiLingual,
        MediaText mediaText) : IRequestHandler<Query, Result<ProductDetail>>
    {
        /// <inheritdoc/>
        public async Task<Result<ProductDetail>> HandleAsync(Query request, CancellationToken ct)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.Id, ct);

            if (product is null) return Error.NotFound("Catalog.ProductNotFound", request.Id);

            return new ProductDetail(
                product.Id,
                product.Sku,
                product.Price,
                product.CategoryId,
                await multiLingual.For(product).GetAllAsync(ct),
                await AttachedFiles.DescribeAsync(media.For(product), mediaText, ct));
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        /// <inheritdoc/>
        public void Map(IEndpointRouteBuilder app) =>
            app.MapGet("/api/products/{id:int}", async (
                    int id,
                    IDispatcher dispatcher,
                    IStringLocalizer<ApiMessages> messages,
                    CancellationToken ct) =>
                (await dispatcher.SendAsync(new Query(id), ct)).ToHttpResult(messages))
               .WithTags("Products")
               .WithSummary("One product with every language's text and its images");
    }
}
