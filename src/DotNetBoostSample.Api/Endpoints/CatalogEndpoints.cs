using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.Media.Core.Models;
using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoostSample.Api.Data;
using DotNetBoostSample.Api.Entities;
using DotNetBoostSample.Api.Integration;
using Microsoft.EntityFrameworkCore;

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
/// </summary>
public static class CatalogEndpoints
{
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
                    Banner = await DescribeAsync(banner, mediaText, ct)
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
            CancellationToken ct) =>
        {
            var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (category is null) return Results.NotFound();

            var banner = await media.For(category).GetByRoleAsync(BannerRole, ct);

            return Results.Ok(new
            {
                category.Id,
                category.Slug,
                // Every language at once — what an editor needs, as languageCode → property → value.
                Translations = await multiLingual.For(category).GetAllAsync(ct),
                Banner = await DescribeAsync(banner, mediaText, ct)
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
            CancellationToken ct) =>
        {
            var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (category is null) return Results.NotFound();

            category.Slug = body.Slug;

            ApplyTranslations(multiLingual.For(category), body.Translations);

            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithSummary("Updates a category's slug and any language's text");

        categories.MapPut("/{id:int}/banner", async (
            int id,
            MediaSelection body,
            AppDbContext db,
            IMediaManager media,
            CancellationToken ct) =>
        {
            var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (category is null) return Results.NotFound();

            var accessor = media.For(category);

            if (body.MediaFileId is null)
            {
                // Clearing the banner detaches the file; the file itself stays in the library,
                // because another entity may be using it.
                var current = await accessor.GetByRoleAsync(BannerRole, ct);
                if (current is not null) await accessor.DetachAsync(current.Id, BannerRole, ct);
                return Results.NoContent();
            }

            // SetAsync replaces whatever was under the role — a category has exactly one banner,
            // which is what DeclareRoles("Category", r => r.One("banner")) declared at startup.
            await accessor.SetAsync(BannerRole, body.MediaFileId.Value, ct);

            return Results.NoContent();
        })
        .WithSummary("Picks an existing media file as the category's banner, or clears it");

        categories.MapDelete("/{id:int}", async (
            int id,
            AppDbContext db,
            IMediaManager media,
            CancellationToken ct) =>
        {
            var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (category is null) return Results.NotFound();

            if (await db.Products.AnyAsync(p => p.CategoryId == id, ct))
                return Results.Conflict(new { message = "Move or delete this category's products first." });

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
                    Primary = primary is null ? null : Describe(primary, text.GetValueOrDefault(primary.Id)),
                    Images = files.Select(f => Describe(f, text.GetValueOrDefault(f.Id)))
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
            CancellationToken ct) =>
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null) return Results.NotFound();

            var accessor = media.For(product);
            var primary = await accessor.GetByRoleAsync(PrimaryRole, ct);
            var gallery = await accessor.ListByRoleAsync(GalleryRole, ct);
            var text = await mediaText.GetManyAsync(
                [.. gallery.Select(f => f.Id).Append(primary?.Id ?? Guid.Empty).Where(g => g != Guid.Empty)], ct);

            return Results.Ok(new
            {
                product.Id,
                product.Sku,
                product.Price,
                product.CategoryId,
                Translations = await multiLingual.For(product).GetAllAsync(ct),
                Primary = primary is null ? null : Describe(primary, text.GetValueOrDefault(primary.Id)),
                Gallery = gallery.Select(f => Describe(f, text.GetValueOrDefault(f.Id)))
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
            CancellationToken ct) =>
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null) return Results.NotFound();

            product.Sku        = body.Sku;
            product.Price      = body.Price;
            product.CategoryId = body.CategoryId;

            ApplyTranslations(multiLingual.For(product), body.Translations);

            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithSummary("Updates a product and any language's text");

        products.MapPut("/{id:int}/primary", async (
            int id,
            MediaSelection body,
            AppDbContext db,
            IMediaManager media,
            CancellationToken ct) =>
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null) return Results.NotFound();

            var accessor = media.For(product);

            if (body.MediaFileId is null)
            {
                var current = await accessor.GetByRoleAsync(PrimaryRole, ct);
                if (current is not null) await accessor.DetachAsync(current.Id, PrimaryRole, ct);
                return Results.NoContent();
            }

            await accessor.SetAsync(PrimaryRole, body.MediaFileId.Value, ct);

            return Results.NoContent();
        })
        .WithSummary("Picks an existing media file as the product's primary image, or clears it");

        products.MapPost("/{id:int}/gallery", async (
            int id,
            MediaSelection body,
            AppDbContext db,
            IMediaManager media,
            CancellationToken ct) =>
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null) return Results.NotFound();
            if (body.MediaFileId is null) return Results.BadRequest(new { message = "mediaFileId is required." });

            // AddAsync appends within the role — a product may have many gallery images.
            await media.For(product).AddAsync(body.MediaFileId.Value, GalleryRole, ct);

            return Results.NoContent();
        })
        .WithSummary("Adds an existing media file to the product's gallery");

        products.MapDelete("/{id:int}/gallery/{mediaFileId:guid}", async (
            int id,
            Guid mediaFileId,
            AppDbContext db,
            IMediaManager media,
            CancellationToken ct) =>
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null) return Results.NotFound();

            await media.For(product).DetachAsync(mediaFileId, GalleryRole, ct);

            return Results.NoContent();
        })
        .WithSummary("Removes an image from the gallery, leaving the file in the library");

        products.MapPost("/{id:int}/images", async (
            int id,
            IFormFile file,
            string? role,
            AppDbContext db,
            IMediaManager media,
            CancellationToken ct) =>
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null) return Results.NotFound();

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
            CancellationToken ct) =>
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null) return Results.NotFound();

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

    private static async Task<object?> DescribeAsync(
        MediaFileInfo? file,
        MediaText mediaText,
        CancellationToken ct) =>
        file is null ? null : Describe(file, await mediaText.GetAsync(file.Id, ct));

    private static object Describe(MediaFileInfo file, MediaTextValues? text) => new
    {
        file.Id,
        file.FileName,
        file.ContentType,
        file.Size,
        AltText = text?.AltText,
        Caption = text?.Caption,
        Url     = $"/api/media/{file.Id}/content"
    };
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
