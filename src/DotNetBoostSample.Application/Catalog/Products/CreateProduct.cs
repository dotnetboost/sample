using DotNetBoostSample.Application.Abstractions.Endpoints;
using DotNetBoostSample.Application.Abstractions.Cqrs;
using DotNetBoostSample.Application.Abstractions.Persistence;
using DotNetBoostSample.Application.Abstractions.Results;
using DotNetBoostSample.Application.Catalog.Shared;
using DotNetBoostSample.Application.Localization;
using DotNetBoostSample.Domain.Catalog;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace DotNetBoostSample.Application.Catalog.Products;

/// <summary>Creates a product in one or more languages.</summary>
public static class CreateProduct
{
    public sealed record Command(
        string Sku,
        decimal Price,
        int CategoryId,
        Dictionary<string, Dictionary<string, string?>>? Translations) : ICommand<Result<ProductCreated>>;

    /// <summary>The new row's id, and the SKU the message is named after.</summary>
    public sealed record ProductCreated(int Id, string Sku);

    /// <summary>
    /// The column shapes, and a price that cannot be negative — <c>HasPrecision(18, 2)</c> bounds
    /// how a price is stored, not its sign. The category id is checked by the handler, because a
    /// validator cannot ask the database.
    /// </summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(p => p.Sku).NotEmpty().MaximumLength(64);
            RuleFor(p => p.Price).GreaterThanOrEqualTo(0);
        }
    }

    public sealed class Handler(
        IAppDbContext db,
        TranslatedText translatedText) : IRequestHandler<Command, Result<ProductCreated>>
    {
        /// <inheritdoc/>
        public async Task<Result<ProductCreated>> HandleAsync(Command request, CancellationToken ct)
        {
            // Asked here rather than left to the foreign key, because only this code knows WHICH id
            // was wrong, so the 404 can name it. Not a guarantee: a category deleted between this
            // query and the insert still ends in the foreign key's refusal, which
            // DbUpdateExceptionHandler answers. The constraint decides; this makes the common case
            // answer properly.
            if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
                return Error.NotFound("Catalog.CategoryNotFound", request.CategoryId);

            var product = new Product
            {
                Sku        = request.Sku,
                Price      = request.Price,
                CategoryId = request.CategoryId
            };

            // AddAsync, because adding is what assigns the HiLo id — see ProductConfiguration.
            await db.Products.AddAsync(product, ct);

            // One SaveChanges for the row, every language's text and its audit entry.
            await translatedText.SaveAsync(db, product, request.Translations, ct);

            return new ProductCreated(product.Id, product.Sku);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        /// <inheritdoc/>
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPost("/api/products", async (
                    Command command,
                    IDispatcher dispatcher,
                    IStringLocalizer<ApiMessages> messages,
                    CancellationToken ct) =>
                (await dispatcher.SendAsync(command, ct))
                    .ToHttpResult(messages, created => Results.Created(
                        $"/api/products/{created.Id}",
                        new CreatedResponse(
                            created.Id,
                            messages["Catalog.ProductCreated", created.Sku].Value))))
               .WithTags("Products")
               .Produces<CreatedResponse>(StatusCodes.Status201Created)
               // The category named in the body does not exist.
               .ProducesProblem(StatusCodes.Status404NotFound)
               .ProducesValidationProblem()
               .WithSummary("Creates a product in one or more languages");
    }
}
