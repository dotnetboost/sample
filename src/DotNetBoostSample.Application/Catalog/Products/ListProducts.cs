using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.Settings.Core.Interfaces;
using DotNetBoostSample.Application.Abstractions.Endpoints;
using DotNetBoostSample.Application.Abstractions.Cqrs;
using DotNetBoostSample.Application.Abstractions.Persistence;
using DotNetBoostSample.Application.Catalog.Shared;
using DotNetBoostSample.Application.Composition;
using DotNetBoostSample.Application.Settings;
using DotNetBoostSample.Domain.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DotNetBoostSample.Application.Catalog.Products;

/// <summary>
/// One page of products, translated, with per-language image alt text.
/// </summary>
public static class ListProducts
{
    /// <param name="Search">Text to look for in the name, the description or the SKU, case aside.
    /// The name and description are matched in the request's language — see the handler.</param>
    public sealed record Query(int? CategoryId, string? Search, int? Page, int? PageSize)
        : IQuery<PagedResult<ProductListItem>>;

    /// <param name="Primary">The file in the <c>primary</c> role, or null.</param>
    /// <param name="Images">Distinct files: one attached as both primary and gallery is one image
    /// here. The per-role view is the detail endpoint's.</param>
    public sealed record ProductListItem(
        int Id,
        string Name,
        string Description,
        string Sku,
        decimal Price,
        int CategoryId,
        AttachedFile? Primary,
        IReadOnlyList<AttachedFile> Images);

    // Deliberately no Validator: a page size is a caller's suggestion, as DotNetBoost.Media treats
    // its own Take, so an out-of-range page is clamped and answered rather than refused.
    // CatalogSettingsTests asserts it.

    public sealed class Handler(
        IAppDbContext db,
        IMediaManager media,
        ISettingManager settings,
        MediaText mediaText) : IRequestHandler<Query, PagedResult<ProductListItem>>
    {
        /// <inheritdoc/>
        public async Task<PagedResult<ProductListItem>> HandleAsync(Query request, CancellationToken ct)
        {
            // Read per request, so an edit on the Settings page applies on the next load.
            var catalog = await settings.For<CatalogSettings>().GetAsync(cancellationToken: ct);

            // PageSize is the default page, not a ceiling: a caller may ask for its own, and the
            // catalogue list sends neither, so there the setting decides.
            var take = Math.Clamp(request.PageSize ?? catalog.PageSize, 1, CatalogRoles.MaxPageSize);
            var skip = (Math.Max(request.Page ?? 1, 1) - 1) * take;

            var query = db.Products.AsQueryable();

            if (request.CategoryId is not null)
                query = query.Where(p => p.CategoryId == request.CategoryId);

            // Written as if Name and Description were columns, though by default they are
            // translation rows: MultiLingual's interceptor rewrites them into the request
            // language's value, in a Where as much as a Select. So "مكتب" finds the desk under
            // Accept-Language: ar and "desk" finds it in English.
            //
            // ToLower rather than ILike, which is Npgsql's and would put the provider in this layer.
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = request.Search.Trim().ToLower();

                query = query.Where(p =>
                    p.Name.ToLower().Contains(term)
                    || p.Description.ToLower().Contains(term)
                    || p.Sku.ToLower().Contains(term));
            }

            // Counted before paging, so the pager cannot offer a page that holds nothing.
            var total = await query.CountAsync(ct);

            var rows = await query.OrderBy(p => p.Id).Skip(skip).Take(take).ToListAsync(ct);

            // Every product's images in one read, per link, so the `primary` entry arrives with
            // the rest.
            var attachedByProduct = await media.ForMany(rows).ListWithRolesAsync(ct);

            // And one for the whole page's alt text.
            var text = await mediaText.GetManyAsync(
                [.. attachedByProduct.Values.SelectMany(files => files.Select(f => f.File.Id))], ct);

            var items = new List<ProductListItem>(rows.Count);

            foreach (var product in rows)
            {
                // Always present: ForMany answers for every owner it was asked about, with an empty
                // list for one that has nothing attached.
                var attached = attachedByProduct[AttachedFiles.OwnerKey(product.Id)];

                var primary = attached.FirstOrDefault(a => a.Role == CatalogRoles.Primary)?.File;

                // Ordered first so the survivor of each group is the same one `ListAsync` would
                // have returned: lowest role, then position.
                var files = attached
                    .OrderBy(a => a.Role).ThenBy(a => a.SortOrder)
                    .GroupBy(a => a.File.Id)
                    .Select(group => group.First().File);

                items.Add(new ProductListItem(
                    product.Id,
                    product.Name,
                    product.Description,
                    product.Sku,
                    product.Price,
                    product.CategoryId,
                    primary is null
                        ? null
                        : AttachedFiles.Describe(primary, text.GetValueOrDefault(primary.Id)),
                    [.. files.Select(f => AttachedFiles.Describe(f, text.GetValueOrDefault(f.Id)))]));
            }

            // The same shape DotNetBoost.Media answers a listing with, so the dashboard pages a
            // catalogue the way it already pages the media library.
            return new PagedResult<ProductListItem>(items, total, skip, take);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        /// <inheritdoc/>
        public void Map(IEndpointRouteBuilder app) =>
            // Returns the page rather than Results.Ok(page) — see ListCategories for why.
            app.MapGet("/api/products", (
                    int? categoryId,
                    string? search,
                    int? page,
                    int? pageSize,
                    IDispatcher dispatcher,
                    CancellationToken ct) =>
                dispatcher.SendAsync(new Query(categoryId, search, page, pageSize), ct))
               .WithTags("Products")
               .WithSummary("One page of products, translated, with per-language image alt text");
    }
}
