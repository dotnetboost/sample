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

namespace DotNetBoostSample.Application.Catalog.Categories;

/// <summary>Deletes a category that has no products.</summary>
public static class DeleteCategory
{
    public sealed record Command(int Id) : ICommand<Result<string>>;

    public sealed class Handler(
        IAppDbContext db,
        IMediaManager media) : IRequestHandler<Command, Result<string>>
    {
        /// <inheritdoc/>
        public async Task<Result<string>> HandleAsync(Command request, CancellationToken ct)
        {
            var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == request.Id, ct);

            if (category is null) return Error.NotFound("Catalog.CategoryNotFound", request.Id);

            if (await db.Products.AnyAsync(p => p.CategoryId == request.Id, ct))
                return Error.Conflict("Catalog.CategoryHasProducts");

            // Detach rather than delete: the banner may be in use elsewhere. The category's own
            // translations go automatically — MultiLingual's SaveChanges interceptor sees the
            // delete.
            await media.For(category).DetachAllAsync(ct);

            // Read before the row goes; the message names it.
            var slug = category.Slug;

            // Auditing's interceptor records the delete with the row's final values, and the entry
            // outlives the row: afterwards it is the only record of what this category said.
            db.Categories.Remove(category);
            await db.SaveChangesAsync(ct);

            return slug;
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        /// <inheritdoc/>
        public void Map(IEndpointRouteBuilder app) =>
            app.MapDelete("/api/categories/{id:int}", async (
                    int id,
                    IDispatcher dispatcher,
                    IStringLocalizer<ApiMessages> messages,
                    CancellationToken ct) =>
                (await dispatcher.SendAsync(new Command(id), ct))
                    .ToHttpResult(messages, slug => Results.Ok(
                        new MessageResponse(messages["Catalog.CategoryDeleted", slug].Value))))
               .WithTags("Categories")
               .Produces<MessageResponse>(StatusCodes.Status200OK)
               .ProducesProblem(StatusCodes.Status404NotFound)
               // The one endpoint with a 409: a category that still has products.
               .ProducesProblem(StatusCodes.Status409Conflict)
               .WithSummary("Deletes a category that has no products");
    }
}
