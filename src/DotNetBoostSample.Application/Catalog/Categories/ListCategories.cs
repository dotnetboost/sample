using DotNetBoost.Media.Core.Interfaces;
using DotNetBoostSample.Application.Abstractions.Endpoints;
using DotNetBoostSample.Application.Abstractions.Cqrs;
using DotNetBoostSample.Application.Abstractions.Persistence;
using DotNetBoostSample.Application.Catalog.Shared;
using DotNetBoostSample.Application.Composition;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DotNetBoostSample.Application.Catalog.Categories;

/// <summary>
/// Every category, translated into the request's language, with its product count and its banner.
///
/// <para>Nothing here reads a translation explicitly: <c>db.Categories.ToListAsync()</c> comes
/// back with <c>Name</c> and <c>Description</c> already filled, because MultiLingual's EF Core
/// interceptors did it. Moving this into a handler changed nothing about that — the interceptors
/// act on the query, not on who is asking.</para>
/// </summary>
public static class ListCategories
{
    /// <summary>No parameters: the category list is short and is not paged.</summary>
    public sealed record Query : IQuery<IReadOnlyList<CategoryListItem>>;

    /// <summary>One row. <c>Products</c> is a count, and <c>Banner</c> is null when none is held.</summary>
    public sealed record CategoryListItem(
        int Id,
        string Name,
        string Description,
        string Slug,
        int Products,
        AttachedFile? Banner);

    public sealed class Handler(
        IAppDbContext db,
        IMediaManager media,
        MediaText mediaText) : IRequestHandler<Query, IReadOnlyList<CategoryListItem>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<CategoryListItem>> HandleAsync(Query request, CancellationToken ct)
        {
            var rows = await db.Categories.OrderBy(c => c.Id).ToListAsync(ct);

            // Every category's product count in one grouped query rather than a COUNT per row. A
            // category with no products is absent from the grouping, hence the lookup's default.
            var productCounts = await db.Products
                .GroupBy(p => p.CategoryId)
                .Select(group => new { CategoryId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(row => row.CategoryId, row => row.Count, ct);

            // Every banner in ONE query. Through media.For(category) this was a GetByRoleAsync per
            // row — the library addressed one owner at a time and had no bulk form of the question,
            // so the cost was paid here in the open and documented as the price of having no
            // integration package. ForMany is the bulk form, and the price is gone.
            //
            // Measured with EF command logging on: this listing is 4 queries whatever it holds —
            // the categories, the grouped product counts, the banners, and one translations read.
            // It was N + 3.
            var banners = await media.ForMany(rows).GetByRoleAsync(CatalogRoles.Banner, ct);

            // One query for every banner's alt text, for the same reason.
            var altText = await mediaText.GetManyAsync(
                [.. banners.Values.Where(b => b is not null).Select(b => b!.Id)], ct);

            return
            [
                .. rows.Select(category => new CategoryListItem(
                    category.Id,
                    category.Name,
                    category.Description,
                    category.Slug,
                    productCounts.GetValueOrDefault(category.Id),

                    // ForMany answers for every owner it was asked about, so this reads a key that
                    // is always there and a value that is null when nothing holds the role.
                    banners[AttachedFiles.OwnerKey(category.Id)] is { } file
                        ? AttachedFiles.Describe(file, altText.GetValueOrDefault(file.Id))
                        : null))
            ];
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        /// <inheritdoc/>
        public void Map(IEndpointRouteBuilder app) =>
            app.MapGet("/api/categories", async (IDispatcher dispatcher, CancellationToken ct) =>
                   Results.Ok(await dispatcher.SendAsync(new Query(), ct)))
               .WithTags("Categories")
               .WithSummary("Categories, translated into the request's language");
    }
}
