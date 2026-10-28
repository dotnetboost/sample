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

namespace DotNetBoostSample.Application.Catalog.Categories;

/// <summary>Creates a category in one or more languages.</summary>
public static class CreateCategory
{
    /// <param name="Translations">languageCode → (propertyName → value), straight from the editor.</param>
    public sealed record Command(
        string Slug,
        Dictionary<string, Dictionary<string, string?>>? Translations) : ICommand<CategoryCreated>;

    /// <summary>The new row's id, and the slug the message is named after.</summary>
    public sealed record CategoryCreated(int Id, string Slug);

    /// <summary>
    /// The shape the slug column already enforces, checked here so a bad slug is a 400 naming the
    /// field rather than a provider error. Uniqueness is left to the index: a "no such slug yet"
    /// query is stale by the time the insert runs.
    /// </summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Slug).NotEmpty().MaximumLength(128);
        }
    }

    public sealed class Handler(
        IAppDbContext db,
        TranslatedText translatedText) : IRequestHandler<Command, CategoryCreated>
    {
        /// <inheritdoc/>
        public async Task<CategoryCreated> HandleAsync(Command request, CancellationToken ct)
        {
            var category = new Category { Slug = request.Slug };

            // AddAsync, because adding is what assigns the HiLo id — see CategoryConfiguration.
            await db.Categories.AddAsync(category, ct);

            // One SaveChanges for the row, every language's text and its audit entry. TranslatedText
            // saves, because with a base language configured the language it runs in decides where
            // each value lands.
            await translatedText.SaveAsync(db, category, request.Translations, ct);

            return new CategoryCreated(category.Id, category.Slug);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        /// <inheritdoc/>
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPost("/api/categories", async (
                    Command command,
                    IDispatcher dispatcher,
                    IStringLocalizer<ApiMessages> messages,
                    CancellationToken ct) =>
                {
                    var created = await dispatcher.SendAsync(command, ct);

                    return Results.Created(
                        $"/api/categories/{created.Id}",
                        new CreatedResponse(
                            created.Id,
                            messages["Catalog.CategoryCreated", created.Slug].Value));
                })
               .WithTags("Categories")
               .Produces<CreatedResponse>(StatusCodes.Status201Created)
               .ProducesValidationProblem()
               .WithSummary("Creates a category in one or more languages");
    }
}
