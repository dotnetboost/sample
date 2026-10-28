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

            // Deletes the files only this product held and detaches the ones other owners still
            // use. Their alt text goes through MediaTextCleanup, which is told exactly which files
            // were deleted, so a surviving image keeps its text.
            await media.For(product).DeleteAllAsync(ct);

            // Read before the row goes; the message names it.
            var sku = product.Sku;

            // MultiLingual's interceptor removes the product's translations, and Auditing's records
            // the delete with the row's final values.
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
               .Produces<MessageResponse>(StatusCodes.Status200OK)
               .ProducesProblem(StatusCodes.Status404NotFound)
               .WithSummary("Deletes a product, the images only it held, and their alt text");
    }
}
