namespace DotNetBoostSample.Application.Catalog.Shared;

/// <summary>
/// One page of something, in the shape DotNetBoost.Media answers a listing with — so the
/// dashboard pages the catalogue the way it already pages the media library.
///
/// <para>Named, where the products listing used to return <c>new { Items, Total, Skip, Take }</c>.
/// An anonymous object has no type for OpenAPI to describe, so the document said the endpoint
/// answered with an untyped object; a generic record describes itself, for every page of every
/// thing.</para>
/// </summary>
/// <param name="Items">This page.</param>
/// <param name="Total">Every row matching the filter, counted before paging — so a pager cannot
/// offer a page that holds nothing.</param>
/// <param name="Skip">Rows skipped to reach this page.</param>
/// <param name="Take">Rows asked for, after the cap in <see cref="CatalogRoles.MaxPageSize"/>.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Skip, int Take);
