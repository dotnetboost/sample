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

namespace DotNetBoostSample.Application.Catalog.Categories;

/// <summary>Updates a category's slug and any language's text.</summary>
public static class UpdateCategory
{
    public sealed record Command(
        int Id,
        string Slug,
        Dictionary<string, Dictionary<string, string?>>? Translations) : ICommand<Result<string>>;

    /// <inheritdoc cref="CreateCategory.Validator"/>
    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Slug).NotEmpty().MaximumLength(128);
        }
    }

    public sealed class Handler(
        IAppDbContext db,
        TranslatedText translatedText) : IRequestHandler<Command, Result<string>>
    {
        /// <summary>Answers with the slug, which is what the message is named after.</summary>
        public async Task<Result<string>> HandleAsync(Command request, CancellationToken ct)
        {
            var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == request.Id, ct);

            if (category is null) return Error.NotFound("Catalog.CategoryNotFound", request.Id);

            category.Slug = request.Slug;

            // Auditing's interceptor records what moved inside this save, so the slug and its entry
            // commit together. An edit to translations alone records nothing: Name and Description
            // carry [AuditIgnore] — see Product.Name.
            await translatedText.SaveAsync(db, category, request.Translations, ct);

            return category.Slug;
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        /// <inheritdoc/>
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPut("/api/categories/{id:int}", async (
                    int id,
                    UpdateCategoryBody body,
                    IDispatcher dispatcher,
                    IStringLocalizer<ApiMessages> messages,
                    CancellationToken ct) =>
                (await dispatcher.SendAsync(new Command(id, body.Slug, body.Translations), ct))
                    .ToHttpResult(messages, slug => Results.Ok(
                        new MessageResponse(messages["Catalog.CategoryUpdated", slug].Value))))
               .WithTags("Categories")
               .Produces<MessageResponse>(StatusCodes.Status200OK)
               .ProducesProblem(StatusCodes.Status404NotFound)
               .ProducesValidationProblem()
               .WithSummary("Updates a category's slug and any language's text");
    }
}

/// <summary>
/// The PUT body, without the id — which arrives in the route.
///
/// <para>Separate from <see cref="UpdateCategory.Command"/> so the route is the only id: binding
/// the command from the body would publish <c>id</c> in the request schema too, free to disagree
/// with the path.</para>
/// </summary>
public sealed record UpdateCategoryBody(
    string Slug,
    Dictionary<string, Dictionary<string, string?>>? Translations);
