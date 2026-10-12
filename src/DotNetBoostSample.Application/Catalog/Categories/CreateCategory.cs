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
    /// The shape the database already enforces, stated where a caller can be told about it.
    ///
    /// <para>The slug column is <c>HasMaxLength(128).IsRequired()</c> with a unique index, so an
    /// empty or oversized slug used to come back as a 500 from the provider. These two rules make
    /// it a 400 with the field named. Uniqueness is deliberately NOT checked here: a query that
    /// says "no such slug yet" is already stale by the time the insert runs, so the index stays the
    /// thing that decides.</para>
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
        EntityText entityText) : IRequestHandler<Command, CategoryCreated>
    {
        /// <inheritdoc/>
        public async Task<CategoryCreated> HandleAsync(Command request, CancellationToken ct)
        {
            var category = new Category { Slug = request.Slug };

            db.Categories.Add(category);

            // One SaveChanges for the row and every language's text: MultiLingual's interceptor
            // writes the queued translations in the same transaction as the insert. The save is
            // EntityText's rather than this handler's because, with a base language configured, the
            // language it runs in decides where each value lands — see that class.
            await entityText.SaveAsync(db, category, request.Translations, ct);

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
