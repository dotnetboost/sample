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
///
/// <para>The whole feature in one file: what is asked, what comes back, what bounds the ask, who
/// answers it, and the route it answers on.</para>
/// </summary>
public static class ListProducts
{
    /// <param name="Search">Text to look for in the name, the description or the SKU, case aside.
    /// The name and description are matched in the request's language — see the handler.</param>
    public sealed record Query(int? CategoryId, string? Search, int? Page, int? PageSize)
        : IQuery<PagedResult<ProductListItem>>;

    /// <param name="Primary">The file in the <c>primary</c> role, or null.</param>
    /// <param name="Images">Distinct files, because <c>images</c> on a LISTING answers "what does
    /// this product have" — a file attached as both primary and gallery is one image, and a count
    /// badge beside it would otherwise read two. The per-role view is the detail endpoint's job,
    /// and that is the only shape carrying a role at all.</param>
    public sealed record ProductListItem(
        int Id,
        string Name,
        string Description,
        string Sku,
        decimal Price,
        int CategoryId,
        AttachedFile? Primary,
        IReadOnlyList<AttachedFile> Images);

    // There is deliberately NO Validator here, and it is the one place in these eleven slices
    // where that is worth saying out loud.
    //
    // A validator bounding Page and PageSize is the obvious thing to add, and it would be wrong:
    // a page size in this API is a caller's SUGGESTION — DotNetBoost.Media says exactly that of
    // its own Take, and the catalogue matches it on purpose — so asking for 99,999 rows is
    // answered with 200 rather than refused. CatalogSettingsTests asserts that, by name. Moving
    // the clamp into a refusal would have been a change to the HTTP contract smuggled in under a
    // restructure, which is the sort of thing a restructure must not do.
    //
    // The clamp therefore stays in the handler, where it was, and the slices that DO carry a
    // Validator — the four writes — use it for shapes the database already enforces, turning a
    // provider-level 500 into a 400 that names the field.

    public sealed class Handler(
        IAppDbContext db,
        IMediaManager media,
        ISettingManager settings,
        MediaText mediaText) : IRequestHandler<Query, PagedResult<ProductListItem>>
    {
        /// <inheritdoc/>
        public async Task<PagedResult<ProductListItem>> HandleAsync(Query request, CancellationToken ct)
        {
            // How the catalogue is presented, editable at runtime on the Settings page. Read per
            // request rather than captured at startup: "change it, reload, done" is the whole
            // promise of DotNetBoost.Settings, and a value read once would quietly not keep it.
            var catalog = await settings.For<CatalogSettings>().GetAsync(cancellationToken: ct);

            // PageSize is the DEFAULT page, not a ceiling. A caller may ask for its own — which is
            // how the dashboard's overview reads enough rows to count across more than one page —
            // and the catalogue list sends neither parameter, so there the setting decides.
            //
            // Clamped rather than refused, for both numbers, which is the contract the comment
            // above the missing Validator explains.
            var take = Math.Clamp(request.PageSize ?? catalog.PageSize, 1, CatalogRoles.MaxPageSize);
            var skip = (Math.Max(request.Page ?? 1, 1) - 1) * take;

            var query = db.Products.AsQueryable();

            if (request.CategoryId is not null)
                query = query.Where(p => p.CategoryId == request.CategoryId);

            // Name and Description have no column in this sample's mode — they are translation
            // rows — and the query is written as if they did anyway. DotNetBoost.MultiLingual's
            // query interceptor rewrites every read of a translatable property, in a Where as much
            // as in a Select, into the value for the request's language with its fallbacks. So
            // this matches what the caller reads: "مكتب" finds the writing desk under
            // Accept-Language: ar, and "desk" finds it in English, from the same line.
            //
            // ToLower on both sides rather than ILike, which is Npgsql's and would put the provider
            // into a layer that does not reference it. Contains is a substring match and not a
            // pattern, so a % or an _ typed into the box is just a character.
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

            // Every product's images in ONE read. Per link rather than per file, so the entry
            // holding `primary` arrives with the rest and needs no GetByRoleAsync of its own.
            //
            // Through media.For(product) this was a call per product, and each cost TWO round
            // trips — the attachment rows, then the files they point at — so a page of fifty
            // measured 2N + 3. ForMany is one, whatever the page holds.
            var attachedByProduct = await media.ForMany(rows).ListWithRolesAsync(ct);

            // One query for the whole page's alt text, for the same reason.
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
