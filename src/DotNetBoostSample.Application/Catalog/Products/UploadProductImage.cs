using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.Media.Core.Models;
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

/// <summary>Uploads an image and attaches it to the product under a role.</summary>
public static class UploadProductImage
{
    /// <summary>
    /// The bytes and what to call them — not an <c>IFormFile</c>, so a test can drive the handler
    /// with a <c>MemoryStream</c>. Size and content type are not checked here: the library refuses
    /// on the limits declared once in <c>AddInfrastructure</c>.
    /// </summary>
    public sealed record Command(
        int ProductId,
        Stream Content,
        string FileName,
        string ContentType,
        string? Role) : ICommand<Result<UploadedImage>>;

    /// <summary>The stored file, as the uploader needs to see it.</summary>
    public sealed record UploadedImage(Guid Id, string FileName, long Size);

    public sealed class Handler(
        IAppDbContext db,
        IMediaManager media) : IRequestHandler<Command, Result<UploadedImage>>
    {
        /// <inheritdoc/>
        public async Task<Result<UploadedImage>> HandleAsync(Command request, CancellationToken ct)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, ct);

            if (product is null) return Error.NotFound("Catalog.ProductNotFound", request.ProductId);

            var uploaded = await media.For(product).UploadAsync(
                new MediaUpload
                {
                    Content     = request.Content,
                    FileName    = request.FileName,
                    ContentType = request.ContentType,
                    Category    = "product-images",
                    Role        = request.Role ?? CatalogRoles.Gallery
                },
                ct);

            return new UploadedImage(uploaded.Id, uploaded.FileName, uploaded.Size);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        /// <inheritdoc/>
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPost("/api/products/{id:int}/images", async (
                    int id,
                    IFormFile file,
                    string? role,
                    IDispatcher dispatcher,
                    IStringLocalizer<ApiMessages> messages,
                    CancellationToken ct) =>
                {
                    await using var stream = file.OpenReadStream();

                    var result = await dispatcher.SendAsync(
                        new Command(id, stream, file.FileName, file.ContentType, role), ct);

                    return result.ToHttpResult(messages, uploaded => Results.Ok(
                        new UploadedImageResponse(
                            uploaded.Id,
                            uploaded.FileName,
                            uploaded.Size,
                            messages["Catalog.ProductImageUploaded", uploaded.FileName].Value)));
                })
               .DisableAntiforgery()
               .WithTags("Products")
               .Produces<UploadedImageResponse>(StatusCodes.Status200OK)
               .ProducesProblem(StatusCodes.Status404NotFound)
               .WithSummary("Uploads an image and attaches it to the product");
    }
}
