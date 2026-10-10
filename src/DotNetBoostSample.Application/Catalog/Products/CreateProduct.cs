using DotNetBoost.MultiLingual.Core.Interfaces;
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
    /// The column shapes, and a price that cannot be negative.
    ///
    /// <para><c>HasPrecision(18, 2)</c> bounds how a price is STORED; nothing bounded its sign, so
    /// a product at −349.00 was accepted and shown.</para>
    ///
    /// <para><b>The category id is not here</b>, because a validator cannot ask the database
    /// anything — it is handed the request and nothing else. The handler checks it, and the
    /// comment there explains why that is worth doing even though the foreign key would also
    /// catch it.</para>
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
        IMultiLingualManager multiLingual) : IRequestHandler<Command, Result<ProductCreated>>
    {
        /// <inheritdoc/>
        public async Task<Result<ProductCreated>> HandleAsync(Command request, CancellationToken ct)
        {
            // The category has to exist, and the foreign key is not a good enough answer on its
            // own. OnDelete(Restrict) does stop an orphan being written — but it reports it as a
            // DbUpdateException wrapping Npgsql's 23503, which reached the caller as
            // "500 An error occurred while processing your request" for the ordinary mistake of
            // sending a categoryId that is not there.
            //
            // Asked here rather than translated from the exception because only this code knows
            // WHICH id was wrong: the answer names it, in the caller's language, through the same
            // key GET /api/categories/{id} already answers 404 with. An exception handler sees the
            // violation but not the request, so the best it could say is "some category".
            //
            // The check is not a guarantee and is not meant to be. A category deleted between
            // this query and the insert below still ends in the foreign key's refusal — the race
            // the slug's uniqueness comment describes, narrow here because DeleteCategory refuses
            // to remove a category that has products. The constraint stays the thing that decides;
            // this only makes the common case answer properly.
            if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
                return Error.NotFound("Catalog.CategoryNotFound", request.CategoryId);

            var product = new Product
            {
                Sku        = request.Sku,
                Price      = request.Price,
                CategoryId = request.CategoryId
            };

            db.Products.Add(product);

            TranslationWrites.Apply(multiLingual.For(product), request.Translations);

            await db.SaveChangesAsync(ct);

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
