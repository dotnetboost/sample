using DotNetBoostSample.Application.Abstractions.Endpoints;
using DotNetBoostSample.Application.Abstractions.Cqrs;
using DotNetBoostSample.Application.Abstractions.Persistence;
using DotNetBoostSample.Application.Abstractions.Results;
using DotNetBoostSample.Application.Catalog.Shared;
using DotNetBoostSample.Application.Localization;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace DotNetBoostSample.Application.Catalog.Products;

/// <summary>Updates a product and any language's text.</summary>
public static class UpdateProduct
{
    public sealed record Command(
        int Id,
        string Sku,
        decimal Price,
        int CategoryId,
        Dictionary<string, Dictionary<string, string?>>? Translations) : ICommand<Result<string>>;

    /// <inheritdoc cref="CreateProduct.Validator"/>
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
        TranslatedText translatedText) : IRequestHandler<Command, Result<string>>
    {
        /// <summary>Answers with the SKU, which is what the message is named after.</summary>
        public async Task<Result<string>> HandleAsync(Command request, CancellationToken ct)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.Id, ct);

            if (product is null) return Error.NotFound("Catalog.ProductNotFound", request.Id);

            // CreateProduct's check, because a PUT can move a product to any category.
            if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
                return Error.NotFound("Catalog.CategoryNotFound", request.CategoryId);

            product.Sku        = request.Sku;
            product.Price      = request.Price;
            product.CategoryId = request.CategoryId;

            // Auditing's interceptor records what moved, old value → new, in this same save. Name
            // and Description carry [AuditIgnore] (see Product.Name), so a save that moved nothing
            // else records nothing.
            await translatedText.SaveAsync(db, product, request.Translations, ct);

            return product.Sku;
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        /// <inheritdoc/>
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPut("/api/products/{id:int}", async (
                    int id,
                    UpdateProductBody body,
                    IDispatcher dispatcher,
                    IStringLocalizer<ApiMessages> messages,
                    CancellationToken ct) =>
                (await dispatcher.SendAsync(
                        new Command(id, body.Sku, body.Price, body.CategoryId, body.Translations), ct))
                    .ToHttpResult(messages, sku => Results.Ok(
                        new MessageResponse(messages["Catalog.ProductUpdated", sku].Value))))
               .WithTags("Products")
               .Produces<MessageResponse>(StatusCodes.Status200OK)
               .ProducesProblem(StatusCodes.Status404NotFound)
               .ProducesValidationProblem()
               .WithSummary("Updates a product and any language's text");
    }
}

/// <inheritdoc cref="Categories.UpdateCategoryBody"/>
public sealed record UpdateProductBody(
    string Sku,
    decimal Price,
    int CategoryId,
    Dictionary<string, Dictionary<string, string?>>? Translations);
