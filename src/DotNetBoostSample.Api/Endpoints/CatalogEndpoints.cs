using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.Media.Core.Models;
using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoostSample.Api.Data;
using DotNetBoostSample.Api.Entities;
using DotNetBoostSample.Api.Integration;
using DotNetBoostSample.Api.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace DotNetBoostSample.Api.Endpoints;

/// <summary>
/// The application's own catalogue — categories and products, with the full edit surface the
/// dashboard needs: list, read, create, update, delete, per-language text, and attaching media by
/// role.
/// <para>
/// Nothing here reads a translation explicitly on the list and detail paths:
/// <c>db.Products.ToListAsync()</c> comes back with <c>Name</c> and <c>Description</c> already
/// filled in the request's language, because MultiLingual's EF Core interceptors did it. The
/// <em>edit</em> paths are the exception — an editor needs every language at once, which is what
/// <c>For(entity).GetAllAsync()</c> returns.
/// </para>
/// <para>
/// Every refusal below carries a message, and the message is localized: the handlers take
/// <see cref="IStringLocalizer{ApiMessages}"/>, which answers in the language the request is being
/// read in. A bare <c>Results.NotFound()</c> says nothing a caller can show a user, and a hardcoded
/// English sentence says it in the wrong language — the content in the same response is translated.
/// </para>
/// </summary>
public static class CatalogEndpoints
{
    /// <summary>
    /// A 404 whose body says which thing was missing, in the caller's language.
    /// <para>
    /// `Results.Problem` rather than `Results.NotFound(text)`: the dashboard reads RFC 7807
    /// problem details, so a refusal here has the same shape as one from any of the three
    /// libraries and needs no special case in the client.
    /// </para>
    /// </summary>
    private static IResult NotFound(IStringLocalizer<ApiMessages> messages, string key, object id) =>
        Results.Problem(detail: messages[key, id], statusCode: StatusCodes.Status404NotFound);

    /// <summary>The role a category's single banner image is attached under.</summary>
    private const string BannerRole = "banner";

    /// <summary>The role a product's single lead image is attached under.</summary>
    private const string PrimaryRole = "primary";

    /// <summary>The role a product's remaining images are attached under.</summary>
    private const string GalleryRole = "gallery";

    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        MapCategories(app.MapGroup("/api/categories").WithTags("Categories"));
        MapProducts(app.MapGroup("/api/products").WithTags("Products"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Categories
    // ─────────────────────────────────────────────────────────────────────────
    private static void MapCategories(RouteGroupBuilder categories)
    {
        categories.MapGet("/", async (
            AppDbContext db,
            IMediaManager media,
            MediaText mediaText,
            CancellationToken ct) =>
        {
            var rows = await db.Categories.OrderBy(c => c.Id).ToListAsync(ct);
            var result = new List<object>(rows.Count);

            foreach (var category in rows)
            {
                var banner = await media.For(category).GetByRoleAsync(BannerRole, ct);

                result.Add(new
                {
                    category.Id,
                    category.Name,
                    category.Description,
                    category.Slug,
                    Products = await db.Products.CountAsync(p => p.CategoryId == category.Id, ct),
                    Banner = await ThumbnailAsync(banner, BannerRole, mediaText, ct)
                });
            }

            return Results.Ok(result);
        })
        .WithSummary("Categories, translated into the request's language");

        categories.MapGet("/{id:int}", async (
            int id,
            AppDbContext db,
            IMediaManager media,
            IMultiLingualManager multiLingual,
            MediaText mediaText,
            IStringLocalizer<ApiMessages> messages,
            CancellationToken ct) =>
        {
            var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (category is null) return NotFound(messages, "Catalog.CategoryNotFound", id);

            return Results.Ok(new
            {
                category.Id,
                category.Slug,
                // Every language at once — what an editor needs, as languageCode → property → value.
                Translations = await multiLingual.For(category).GetAllAsync(ct),
                // Every attached file WITH the role it is attached under, which is what the
                // editor's role boxes are drawn from. The roles themselves come from the media
                // library's own GET /api/media/roles/Category — declared at startup, not here.
                Images = await AttachedFiles.DescribeAsync(media.For(category), mediaText, ct)
            });
        })
        .WithSummary("One category with every language's text and its banner");

        categories.MapPost("/", async (
            CategoryWrite body,
            AppDbContext db,
            IMultiLingualManager multiLingual,
            CancellationToken ct) =>
        {
            var category = new Category { Slug = body.Slug };
            db.Categories.Add(category);

            ApplyTranslations(multiLingual.For(category), body.Translations);

            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/categories/{category.Id}", new { category.Id });
        })
        .WithSummary("Creates a category in one or more languages");

        categories.MapPut("/{id:int}", async (
            int id,
            CategoryWrite body,
            AppDbContext db,
            IMultiLingualManager multiLingual,
            IStringLocalizer<ApiMessages> messages,
            CancellationToken ct) =>
        {
            var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (category is null) return NotFound(messages, "Catalog.CategoryNotFound", id);

            category.Slug = body.Slug;

            ApplyTranslations(multiLingual.For(category), body.Translations);

            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithSummary("Updates a category's slug and any language's text");

        categories.MapDelete("/{id:int}", async (
            int id,
            AppDbContext db,
            IMediaManager media,
            IStringLocalizer<ApiMessages> messages,
            CancellationToken ct) =>
        {
            var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (category is null) return NotFound(messages, "Catalog.CategoryNotFound", id);

            // Was an English literal in an ad-hoc `{ message }` shape, which the dashboard's
            // problem-details reader could not see at all. Now localized, and RFC 7807 like the rest.
            if (await db.Products.AnyAsync(p => p.CategoryId == id, ct))
                return Results.Problem(
                    detail: messages["Catalog.CategoryHasProducts"],
                    statusCode: StatusCodes.Status409Conflict);

            // Detach rather than delete: the banner may be in use elsewhere. The category's own
            // translations go automatically — MultiLingual's SaveChanges interceptor sees the
            // delete.
            await media.For(category).DetachAllAsync(ct);

            db.Categories.Remove(category);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithSummary("Deletes a category that has no products");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Products
    // ─────────────────────────────────────────────────────────────────────────
    private static void MapProducts(RouteGroupBuilder products)
    {
        products.MapGet("/", async (
            AppDbContext db,
            IMediaManager media,
            MediaText mediaText,
            int? categoryId,
            CancellationToken ct) =>
        {
            var query = db.Products.AsQueryable();

            if (categoryId is not null)
                query = query.Where(p => p.CategoryId == categoryId);

            var rows = await query.OrderBy(p => p.Id).ToListAsync(ct);
            var result = new List<object>(rows.Count);

            foreach (var product in rows)
            {
                var files = await media.For(product).ListAsync(ct);
                var text = await mediaText.GetManyAsync([.. files.Select(f => f.Id)], ct);
                var primary = await media.For(product).GetByRoleAsync(PrimaryRole, ct);

                result.Add(new
                {
                    product.Id,
                    product.Name,
                    product.Description,
                    product.Sku,
                    product.Price,
                    product.CategoryId,
                    Primary = await ThumbnailAsync(primary, PrimaryRole, mediaText, ct),
                    Images = files.Select(f => AttachedFiles.Describe(f, string.Empty, 0, text.GetValueOrDefault(f.Id)))
                });
            }

            return Results.Ok(result);
        })
        .WithSummary("Products with translated text and per-language image alt text");

        products.MapGet("/{id:int}", async (
            int id,
            AppDbContext db,
            IMediaManager media,
            IMultiLingualManager multiLingual,
            MediaText mediaText,
            IStringLocalizer<ApiMessages> messages,
            CancellationToken ct) =>
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null) return NotFound(messages, "Catalog.ProductNotFound", id);

            return Results.Ok(new
            {
                product.Id,
                product.Sku,
                product.Price,
                product.CategoryId,
                Translations = await multiLingual.For(product).GetAllAsync(ct),
                Images = await AttachedFiles.DescribeAsync(media.For(product), mediaText, ct)
            });
        })
        .WithSummary("One product with every language's text and its images");

        products.MapPost("/", async (
            ProductWrite body,
            AppDbContext db,
            IMultiLingualManager multiLingual,
            CancellationToken ct) =>
        {
            var product = new Product
            {
                Sku        = body.Sku,
                Price      = body.Price,
                CategoryId = body.CategoryId
            };

            db.Products.Add(product);

            ApplyTranslations(multiLingual.For(product), body.Translations);

            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/products/{product.Id}", new { product.Id });
        })
        .WithSummary("Creates a product in one or more languages");

        products.MapPut("/{id:int}", async (
            int id,
            ProductWrite body,
            AppDbContext db,
            IMultiLingualManager multiLingual,
            IStringLocalizer<ApiMessages> messages,
            CancellationToken ct) =>
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null) return NotFound(messages, "Catalog.ProductNotFound", id);

            product.Sku        = body.Sku;
            product.Price      = body.Price;
            product.CategoryId = body.CategoryId;

            ApplyTranslations(multiLingual.For(product), body.Translations);

            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithSummary("Updates a product and any language's text");

        products.MapPost("/{id:int}/images", async (
            int id,
            IFormFile file,
            string? role,
            AppDbContext db,
            IMediaManager media,
            IStringLocalizer<ApiMessages> messages,
            CancellationToken ct) =>
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null) return NotFound(messages, "Catalog.ProductNotFound", id);

            await using var stream = file.OpenReadStream();

            var uploaded = await media.For(product).UploadAsync(
                new MediaUpload
                {
                    Content     = stream,
                    FileName    = file.FileName,
                    ContentType = file.ContentType,
                    Category    = "product-images",
                    Role        = role ?? GalleryRole
                },
                ct);

            return Results.Ok(new { uploaded.Id, uploaded.FileName, uploaded.Size });
        })
        .DisableAntiforgery()
        .WithSummary("Uploads an image and attaches it to the product");

        products.MapDelete("/{id:int}", async (
            int id,
            AppDbContext db,
            IMediaManager media,
            MediaText mediaText,
            IStringLocalizer<ApiMessages> messages,
            CancellationToken ct) =>
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null) return NotFound(messages, "Catalog.ProductNotFound", id);

            // Nothing cascades across library boundaries — the honest cost of having no
            // integration package, which is why this is written out rather than hidden.
            var files = await media.For(product).ListAsync(ct);

            foreach (var file in files)
            {
                await mediaText.DeleteAsync(file.Id, ct);
                await media.DeleteAsync(file.Id, ct);
            }

            // The product's own translations ARE removed automatically: MultiLingual's
            // SaveChanges interceptor sees the entity being deleted.
            db.Products.Remove(product);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithSummary("Deletes a product, its images, and their alt text");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Shared helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Queues one language's values per call. Uses the by-name overload because the property names
    /// arrive in a request body rather than being known at compile time.
    /// </summary>
    private static void ApplyTranslations<T>(
        ITranslationAccessor<T> accessor,
        Dictionary<string, Dictionary<string, string?>>? translations)
        where T : class
    {
        foreach (var (language, values) in translations ?? [])
            accessor.Set(language, values);
    }

    /// <summary>
    /// One file for a list thumbnail, with its alt text resolved. The role is passed in because a
    /// thumbnail is fetched by role — a category's banner, a product's primary image.
    /// </summary>
    private static async Task<object?> ThumbnailAsync(
        MediaFileInfo? file,
        string role,
        MediaText mediaText,
        CancellationToken ct) =>
        file is null
            ? null
            : AttachedFiles.Describe(file, role, 0, await mediaText.GetAsync(file.Id, ct));
}

/// <summary>Picking an existing library file for a role. A null id clears the role.</summary>
public sealed record MediaSelection(Guid? MediaFileId);

/// <summary>Translations as languageCode → (propertyName → value), straight from the editor.</summary>
public sealed record CategoryWrite(
    string Slug,
    Dictionary<string, Dictionary<string, string?>>? Translations);

/// <inheritdoc cref="CategoryWrite"/>
public sealed record ProductWrite(
    string Sku,
    decimal Price,
    int CategoryId,
    Dictionary<string, Dictionary<string, string?>>? Translations);
