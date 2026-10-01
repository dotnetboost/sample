using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoostSample.Api.Data;
using DotNetBoostSample.Api.Entities;
using DotNetBoostSample.Api.Integration;
using Microsoft.EntityFrameworkCore;

namespace DotNetBoostSample.Api.Endpoints;

/// <summary>
/// The application's own catalogue endpoints — categories and products.
/// <para>
/// Nothing here reads a translation explicitly. <c>db.Products.ToListAsync()</c> comes back with
/// <c>Name</c> and <c>Description</c> already filled in the request's language, because
/// MultiLingual's EF Core interceptors did it. Send <c>Accept-Language: ar</c> and the same
/// endpoint answers in Arabic.
/// </para>
/// </summary>
public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var categories = app.MapGroup("/api/categories").WithTags("Categories");
        var products   = app.MapGroup("/api/products").WithTags("Products");

        // ── Categories ───────────────────────────────────────────────────────
        categories.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
        {
            var rows = await db.Categories
                .OrderBy(c => c.Id)
                .Select(c => new { c.Id, c.Name, c.Description, c.Slug, Products = c.Products.Count })
                .ToListAsync(ct);

            return Results.Ok(rows);
        })
        .WithSummary("Categories, translated into the request's language");

        categories.MapPost("/", async (
            CategoryWrite body,
            AppDbContext db,
            IMultiLingualManager multiLingual,
            CancellationToken ct) =>
        {
            var category = new Category { Slug = body.Slug };

            // The default-language values are written like any other property; other languages go
            // through the accessor, which queues them for the same SaveChanges.
            category.Name        = body.Name;
            category.Description = body.Description;

            db.Categories.Add(category);

            foreach (var (language, values) in body.Translations ?? [])
            {
                multiLingual.For(category)
                    .Set(language, c => c.Name, values.Name)
                    .Set(language, c => c.Description, values.Description);
            }

            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/categories/{category.Id}", new { category.Id });
        })
        .WithSummary("Creates a category, optionally in several languages at once");

        // ── Products ─────────────────────────────────────────────────────────
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
                // Media addresses the product by (entity name, key) — derived from the instance.
                var files = await media.For(product).ListAsync(ct);

                // And the alt text for those files comes from MultiLingual, via the glue service.
                var text = await mediaText.GetManyAsync([.. files.Select(f => f.Id)], ct);

                result.Add(new
                {
                    product.Id,
                    product.Name,          // translated
                    product.Description,   // translated
                    product.Sku,           // not translated, by design
                    product.Price,
                    product.CategoryId,
                    Images = files.Select(f => new
                    {
                        f.Id,
                        f.FileName,
                        f.ContentType,
                        f.Size,
                        AltText = text[f.Id].AltText,    // ← Media row + MultiLingual row, joined here
                        Caption = text[f.Id].Caption,
                        Url     = $"/api/media/{f.Id}/content"
                    })
                });
            }

            return Results.Ok(result);
        })
        .WithSummary("Products with translated text and per-language image alt text");

        products.MapPost("/", async (
            ProductWrite body,
            AppDbContext db,
            IMultiLingualManager multiLingual,
            CancellationToken ct) =>
        {
            var product = new Product
            {
                Sku         = body.Sku,
                Price       = body.Price,
                CategoryId  = body.CategoryId,
                Name        = body.Name,
                Description = body.Description
            };

            db.Products.Add(product);

            foreach (var (language, values) in body.Translations ?? [])
            {
                multiLingual.For(product)
                    .Set(language, p => p.Name, values.Name)
                    .Set(language, p => p.Description, values.Description);
            }

            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/products/{product.Id}", new { product.Id });
        })
        .WithSummary("Creates a product, optionally in several languages at once");

        // ── Uploading an image for a product ─────────────────────────────────
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
                new DotNetBoost.Media.Core.Models.MediaUpload
                {
                    Content     = stream,
                    FileName    = file.FileName,
                    ContentType = file.ContentType,
                    Category    = "product-images",
                    Role        = role ?? "gallery"
                },
                ct);

            return Results.Ok(new
            {
                uploaded.Id,
                uploaded.FileName,
                uploaded.Size,
                Next = $"POST /api/media-text/{uploaded.Id} to add alt text per language"
            });
        })
        .DisableAntiforgery()
        .WithSummary("Uploads an image and attaches it to the product");

        // ── Deleting a product: three libraries' rows, cleaned up explicitly ──
        products.MapDelete("/{id:int}", async (
            int id,
            AppDbContext db,
            IMediaManager media,
            MediaText mediaText,
            CancellationToken ct) =>
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null) return Results.NotFound();

            // The media files attached to it, and the alt text belonging to those files.
            // Nothing cascades across library boundaries — which is the honest cost of having no
            // integration package, and is why this is written out rather than hidden.
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
}

public sealed record LocalisedText(string Name, string Description);

public sealed record CategoryWrite(
    string Slug,
    string Name,
    string Description,
    Dictionary<string, LocalisedText>? Translations);

public sealed record ProductWrite(
    string Sku,
    decimal Price,
    int CategoryId,
    string Name,
    string Description,
    Dictionary<string, LocalisedText>? Translations);
