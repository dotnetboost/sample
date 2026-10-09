using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoostSample.Application.Abstractions.Endpoints;
using DotNetBoostSample.Application.Abstractions.Cqrs;
using DotNetBoostSample.Application.Abstractions.Persistence;
using DotNetBoostSample.Application.Catalog.Shared;
using DotNetBoostSample.Application.Localization;
using DotNetBoostSample.Domain.Catalog;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace DotNetBoostSample.Application.Catalog.Products;

/// <summary>Creates a product in one or more languages.</summary>
public static class CreateProduct
{
    public sealed record Command(
        string Sku,
        decimal Price,
        int CategoryId,
        Dictionary<string, Dictionary<string, string?>>? Translations) : ICommand<ProductCreated>;

    /// <summary>The new row's id, and the SKU the message is named after.</summary>
    public sealed record ProductCreated(int Id, string Sku);

    /// <summary>
    /// The column shapes, and a price that cannot be negative.
    ///
    /// <para><c>HasPrecision(18, 2)</c> bounds how a price is STORED; nothing bounded its sign, so
    /// a product at −349.00 was accepted and shown. The category id is checked by the foreign key
    /// rather than here, for the same reason the slug's uniqueness is: a row that exists when the
    /// handler looks may not exist when the insert runs.</para>
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
        IMultiLingualManager multiLingual) : IRequestHandler<Command, ProductCreated>
    {
        /// <inheritdoc/>
        public async Task<ProductCreated> HandleAsync(Command request, CancellationToken ct)
        {
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
                {
                    var created = await dispatcher.SendAsync(command, ct);

                    return Results.Created(
                        $"/api/products/{created.Id}",
                        new CreatedResponse(
                            created.Id,
                            messages["Catalog.ProductCreated", created.Sku].Value));
                })
               .WithTags("Products")
               .Produces<CreatedResponse>(StatusCodes.Status201Created)
               .ProducesValidationProblem()
               .WithSummary("Creates a product in one or more languages");
    }
}
