using DotNetBoost.Media.Core.Interfaces;
using DotNetBoostSample.Application.Abstractions.Endpoints;
using DotNetBoostSample.Application.Abstractions.Cqrs;
using DotNetBoostSample.Application.Abstractions.Persistence;
using DotNetBoostSample.Application.Abstractions.Results;
using DotNetBoostSample.Application.Catalog.Shared;
using DotNetBoostSample.Application.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace DotNetBoostSample.Application.Catalog.Products;

/// <summary>Deletes a product, the images only it held, and their alt text.</summary>
public static class DeleteProduct
{
    public sealed record Command(int Id) : ICommand<Result<string>>;

    public sealed class Handler(
        IAppDbContext db,
        IMediaManager media) : IRequestHandler<Command, Result<string>>
    {
        /// <inheritdoc/>
        public async Task<Result<string>> HandleAsync(Command request, CancellationToken ct)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.Id, ct);

            if (product is null) return Error.NotFound("Catalog.ProductNotFound", request.Id);

            // Deletes the files this product was the ONLY holder of, and merely detaches the ones
            // other products still reference — which is the difference between this and the loop it
            // replaced. That loop called IMediaManager.DeleteAsync per file, which removes a file
            // from every owner, so deleting one product pulled shared images out of the rest.
            //
            // The alt text is not deleted here: MediaTextCleanup is registered as an
            // IMediaFilesDeletedHandler and is told exactly which files went, so a detached-but-
            // surviving image keeps its text and a deleted one loses it.
            await media.For(product).DeleteAllAsync(ct);

            // Read before the row goes, for the same reason as the category's slug.
            var sku = product.Sku;

            // The product's own translations ARE removed automatically: MultiLingual's SaveChanges
            // interceptor sees the entity being deleted.
            db.Products.Remove(product);
            await db.SaveChangesAsync(ct);

            return sku;
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        /// <inheritdoc/>
        public void Map(IEndpointRouteBuilder app) =>
            app.MapDelete("/api/products/{id:int}", async (
                    int id,
                    IDispatcher dispatcher,
                    IStringLocalizer<ApiMessages> messages,
                    CancellationToken ct) =>
                (await dispatcher.SendAsync(new Command(id), ct))
                    .ToHttpResult(messages, sku => Results.Ok(
                        new MessageResponse(messages["Catalog.ProductDeleted", sku].Value))))
               .WithTags("Products")
               .WithSummary("Deletes a product, the images only it held, and their alt text");
    }
}
