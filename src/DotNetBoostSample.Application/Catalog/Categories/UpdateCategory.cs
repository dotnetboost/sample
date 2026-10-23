using DotNetBoost.Auditing.Core.Interfaces;
using DotNetBoost.Auditing.Core.Services;
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
        TranslatedText translatedText,
        IAuditManager audit) : IRequestHandler<Command, Result<string>>
    {
        /// <summary>Answers with the slug, which is what the message is named after.</summary>
        public async Task<Result<string>> HandleAsync(Command request, CancellationToken ct)
        {
            var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == request.Id, ct);

            if (category is null) return Error.NotFound("Catalog.CategoryNotFound", request.Id);

            // The before-image, which the explicit path has instead of a change tracker's original
            // values. A second read of a row already in memory, and the library's documentation
            // says plainly that this is what it costs: AuditDiff compares two instances, so there
            // has to be a second instance.
            //
            // AsNoTracking, or EF would hand back the tracked entity — the same object that is
            // about to be mutated, which would make every diff empty.
            var before = await db.Categories.AsNoTracking().FirstAsync(c => c.Id == request.Id, ct);

            category.Slug = request.Slug;

            // Recorded BEFORE the save, which is what keeps the change and its trail in one
            // transaction: the EF Core store tracks the entry rather than writing it, so
            // SaveAsync below commits the slug and the entry together or neither.
            //
            // Guarded on Count, because the engine refuses to write an update of nothing and a PUT
            // that changed nothing is not an error. It happens: the editor posts the slug back
            // unchanged whenever only the translations were edited. Name and Description do move
            // in that case and are in this list — they are dropped by the engine instead, for the
            // reason written on Product.Name, so no entry is written either way.
            var changes = AuditDiff.Between(before, category);

            if (changes.Count > 0) await audit.For(category).UpdatedAsync(changes, ct: ct);

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
/// <para>A separate type from <see cref="UpdateCategory.Command"/> on purpose. Binding the command
/// straight from the body would put <c>id</c> in the published request schema as well as in the
/// path, and leave the two free to disagree: a body saying <c>"id": 9</c> on
/// <c>PUT /api/categories/3</c> has to be either ignored or honoured, and both are surprising. The
/// route is the only id, and the endpoint is where the two halves meet.</para>
/// </summary>
public sealed record UpdateCategoryBody(
    string Slug,
    Dictionary<string, Dictionary<string, string?>>? Translations);
