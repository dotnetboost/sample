using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.Media.Core.Metadata;
using DotNetBoost.Media.Core.Models;
using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoost.Settings.Core.Interfaces;
using DotNetBoostSample.Api.Data;
using DotNetBoostSample.Api.Entities;
using DotNetBoostSample.Api.Integration;
using DotNetBoostSample.Api.Localization;
using DotNetBoostSample.Api.Settings;
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

    /// <summary>
    /// The largest page the catalogue will answer with, whatever is asked for.
    /// <para>
    /// A page size is a caller's suggestion — DotNetBoost.Media says exactly that of its own
    /// <c>Take</c> — and an unbounded one is how a listing endpoint becomes a way to exhaust the
    /// server's memory. <c>CatalogSettings.PageSize</c> is the default within this; it is not a
    /// way past it.
    /// </para>
    /// </summary>
    private const int MaxPageSize = 200;

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
                    Banner = await ThumbnailAsync(banner, mediaText, ct)
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
            ISettingManager settings,
            MediaText mediaText,
            int? categoryId,
            int? page,
            int? pageSize,
            CancellationToken ct) =>
        {
            // How the catalogue is presented, editable at runtime on the Settings page. Read per
            // request rather than captured at startup: "change it, reload, done" is the whole
            // promise of DotNetBoost.Settings, and a value read once would quietly not keep it.
            var catalog = await settings.For<CatalogSettings>().GetAsync(cancellationToken: ct);

            // PageSize is the DEFAULT page, not a ceiling. A caller may ask for its own — which is
            // how the dashboard's overview reads enough rows to count across more than one page —
            // and the catalogue list sends neither parameter, so there the setting decides.
            var take = Math.Clamp(pageSize ?? catalog.PageSize, 1, MaxPageSize);
            var skip = (Math.Max(page ?? 1, 1) - 1) * take;

            var query = db.Products.AsQueryable();

            if (categoryId is not null)
                query = query.Where(p => p.CategoryId == categoryId);

            // The setting's other half: a product with no image is not listed at all.
            if (!catalog.ShowProductsWithoutImages)
                query = query.Where(p => ProductsWithAnImage(db).Contains(p.Id.ToString()));

            // Counted AFTER the filter, so a page is a page of what is actually being listed and
            // the pager cannot offer a page that holds nothing.
            var total = await query.CountAsync(ct);

            var rows = await query.OrderBy(p => p.Id).Skip(skip).Take(take).ToListAsync(ct);
            var result = new List<object>(rows.Count);

            // Still three media round trips per row, and now bounded by `take` rather than by the
            // size of the table — which is the second thing PageSize buys.
            foreach (var product in rows)
            {
                // One read, two answers. ListWithRolesAsync is per LINK rather than per file, so
                // the entry holding `primary` is already in what came back and does not need a
                // GetByRoleAsync of its own — two round trips a row instead of three.
                var attached = await media.For(product).ListWithRolesAsync(ct);

                var text = await mediaText.GetManyAsync([.. attached.Select(a => a.File.Id)], ct);

                var primary = attached.FirstOrDefault(a => a.Role == PrimaryRole)?.File;

                // Distinct files, because `images` on a LISTING answers "what does this product
                // have" — a file attached as both primary and gallery is one image, and the count
                // badge beside it would otherwise read two. Ordered first so the survivor of each
                // group is the same one `ListAsync` would have returned: lowest role, then
                // position. The per-role view is the detail endpoint's job, and that is the only
                // shape carrying a role at all.
                var files = attached
                    .OrderBy(a => a.Role).ThenBy(a => a.SortOrder)
                    .GroupBy(a => a.File.Id)
                    .Select(group => group.First().File);

                result.Add(new
                {
                    product.Id,
                    product.Name,
                    product.Description,
                    product.Sku,
                    product.Price,
                    product.CategoryId,
                    Primary = primary is null
                        ? null
                        : AttachedFiles.Describe(primary, text.GetValueOrDefault(primary.Id)),
                    Images = files.Select(f => AttachedFiles.Describe(f, text.GetValueOrDefault(f.Id)))
                });
            }

            // The same shape DotNetBoost.Media answers a listing with, so the dashboard pages a
            // catalogue the way it already pages the media library.
            return Results.Ok(new { Items = result, Total = total, Skip = skip, Take = take });
        })
        .WithSummary("One page of products, translated, with per-language image alt text");

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
    /// The ids of every product with at least one live file attached — the subquery behind
    /// <c>CatalogSettings.ShowProductsWithoutImages</c>.
    ///
    /// <para><b>Why this reads the media library's rows instead of calling it.</b> "Has an image"
    /// is a fact about DotNetBoost.Media's attachments, and the filter asks it of every product at
    /// once, before a single row can be paged. The library addresses one owner at a time —
    /// <c>media.For(product)</c> — and ships no bulk owner API, so going through the interface
    /// means a query per product across the whole table just to decide what the first page holds.
    /// That is not a filter; it is a scan with a filter printed on it.</para>
    ///
    /// <para>So this is the one place in the application that reads a library's own table rather
    /// than its interface, and the cost is named rather than hidden: it depends on
    /// <see cref="MediaAttachment"/>'s column shape, which a package upgrade may change and
    /// <c>IMediaAccessor</c> may not. Nothing is being reached around — <see cref="AppDbContext"/>
    /// already hosts Media's entities, because <c>ApplyMedia()</c> asks it to — the interface
    /// simply cannot express this question. If it grows one, this method is the only caller to
    /// change.</para>
    ///
    /// <para><b>The attachment table alone is the right answer, and that is a documented
    /// guarantee rather than a guess.</b> Deleting a file removes every attachment of it outright
    /// <i>even under soft delete</i> — the row may survive with <c>DeletedAt</c> set, as a record
    /// that the file existed, but nothing is attached to it any more. So there is no live-file join
    /// to make here: an attachment row existing IS the file being usable. This is the assumption to
    /// re-read if Media's delete semantics ever change, and it is the reason this query is one
    /// statement rather than a join. (The first version of this method did join, on
    /// <c>!f.IsDeleted</c> — which does not even translate, because <c>IsDeleted</c> is computed
    /// from <c>DeletedAt</c> and is not a column.)</para>
    ///
    /// <para>The owner id is compared as text because that is what an attachment stores. Both
    /// libraries format an <c>int</c> key the same way and tests/KeyFormatAgreementTests.cs
    /// asserts it, so <c>p.Id.ToString()</c> is the same string the attachment was written with.
    /// The entity name is asked of Media rather than written out, so it cannot drift from the one
    /// <c>media.For(product)</c> attaches under.</para>
    /// </summary>
    private static IQueryable<string> ProductsWithAnImage(AppDbContext db)
    {
        var ownerName = MediaEntityNames.For(typeof(Product));

        return db.MediaAttachments
            .Where(a => a.OwnerEntityName == ownerName)
            .Select(a => a.OwnerEntityId)
            .Distinct();
    }

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
    /// One file for a list thumbnail, with its alt text resolved.
    /// <para>
    /// It no longer carries the role it was fetched by. The field it lands in already says which
    /// one — <c>banner</c>, <c>primary</c>, <c>logo</c> — so repeating it told the caller nothing it
    /// did not know, while making the listing's file shape look like the detail endpoint's
    /// role-slot shape without being it.
    /// </para>
    /// </summary>
    private static async Task<object?> ThumbnailAsync(
        MediaFileInfo? file,
        MediaText mediaText,
        CancellationToken ct) =>
        file is null
            ? null
            : AttachedFiles.Describe(file, await mediaText.GetAsync(file.Id, ct));
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
