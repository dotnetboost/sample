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
/// <para>
/// <b>So does every write that succeeds.</b> <c>POST</c>, <c>PUT</c> and <c>DELETE</c> each answer
/// with a <c>message</c> naming what they did, from the same resource files and in the same
/// language as the refusal they might have been instead. That is why the two updates and the two
/// deletes return <c>200 OK</c> rather than <c>204 No Content</c>: a 204 is defined to have no
/// body, so it has nowhere to put the sentence. The caller is free to ignore it — the id on a
/// create is still the part that matters — but a caller that wants to say what happened no longer
/// has to write the sentence itself, in the one language its own bundle happens to be in.
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

    /// <summary>
    /// What a write that succeeded answers with: <c>200 OK</c> and a localized sentence saying what
    /// it did, under the same <c>message</c> name whichever write it was.
    /// <para>
    /// The argument is the entity's slug or SKU rather than its id, which is what separates these
    /// keys from the 404s above: a 404 is about an id that arrived in a URL and is the only handle
    /// the caller has, while this message is read by whoever just typed that slug into the editor.
    /// "Category phone-cases was saved." tells them their edit landed on the row they meant;
    /// "Category 7 was saved." makes them go and check.
    /// </para>
    /// </summary>
    private static IResult Wrote(IStringLocalizer<ApiMessages> messages, string key, object subject) =>
        Results.Ok(new { Message = messages[key, subject].Value });

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

            // Every category's product count in one grouped query rather than a COUNT per row. A
            // category with no products is absent from the grouping, hence the lookup's default.
            var productCounts = await db.Products
                .GroupBy(p => p.CategoryId)
                .Select(group => new { CategoryId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(row => row.CategoryId, row => row.Count, ct);

            var result = new List<object>(rows.Count);

            foreach (var category in rows)
            {
                // Still per category: the banner is an attachment, and the media library addresses
                // one owner at a time. Nothing joins the two tables, which is the point — so the
                // cost of that is paid here, in the open.
                var banner = await media.For(category).GetByRoleAsync(BannerRole, ct);

                result.Add(new
                {
                    category.Id,
                    category.Name,
                    category.Description,
                    category.Slug,
                    Products = productCounts.GetValueOrDefault(category.Id),
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
            IStringLocalizer<ApiMessages> messages,
            CancellationToken ct) =>
        {
            var category = new Category { Slug = body.Slug };
            db.Categories.Add(category);

            ApplyTranslations(multiLingual.For(category), body.Translations);

            await db.SaveChangesAsync(ct);

            // The id AND the message: a 201 is the one write whose body already had something the
            // caller needs, and the sentence is added beside it rather than in place of it.
            return Results.Created(
                $"/api/categories/{category.Id}",
                new
                {
                    category.Id,
                    Message = messages["Catalog.CategoryCreated", category.Slug].Value
                });
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

            return Wrote(messages, "Catalog.CategoryUpdated", category.Slug);
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

            // Read before the row goes: naming the deleted thing is the whole point of the message,
            // and `category.Slug` is only reliable while the entity is still there to read it from.
            var slug = category.Slug;

            db.Categories.Remove(category);
            await db.SaveChangesAsync(ct);

            return Wrote(messages, "Catalog.CategoryDeleted", slug);
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
                var attachments = media.For(product);

                var files = await attachments.ListAsync(ct);
                var text = await mediaText.GetManyAsync([.. files.Select(f => f.Id)], ct);
                var primary = await attachments.GetByRoleAsync(PrimaryRole, ct);

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
            IStringLocalizer<ApiMessages> messages,
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

            return Results.Created(
                $"/api/products/{product.Id}",
                new
                {
                    product.Id,
                    Message = messages["Catalog.ProductCreated", product.Sku].Value
                });
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

            return Wrote(messages, "Catalog.ProductUpdated", product.Sku);
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

            // Named after the file rather than the product's SKU: the file is the thing that just
            // arrived, and it is the only one of the two the uploader can get wrong.
            return Results.Ok(new
            {
                uploaded.Id,
                uploaded.FileName,
                uploaded.Size,
                Message = messages["Catalog.ProductImageUploaded", uploaded.FileName].Value
            });
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

            // Read before the row goes, for the same reason as the category's slug.
            var sku = product.Sku;

            // The product's own translations ARE removed automatically: MultiLingual's
            // SaveChanges interceptor sees the entity being deleted.
            db.Products.Remove(product);
            await db.SaveChangesAsync(ct);

            return Wrote(messages, "Catalog.ProductDeleted", sku);
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
