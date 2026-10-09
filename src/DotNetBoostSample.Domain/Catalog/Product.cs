using DotNetBoost.MultiLingual.Core.Attributes;

namespace DotNetBoostSample.Domain.Catalog;

/// <summary>
/// The sample's second entity. Media files attach to it, and its name and description are
/// translated — two libraries acting on one type that neither of them has ever heard of.
/// <para>
/// There is no <c>MediaFileId</c> column and no <c>Images</c> collection here: attachments live in
/// the media library's own table, keyed by the entity name <c>"Product"</c> and this row's
/// <see cref="Id"/>. Adding media to a new entity type adds no columns and no migrations.
/// </para>
/// </summary>
[MultiLingual]
public class Product
{
    public int Id { get; set; }

    /// <summary>Translatable.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Translatable.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>NOT translatable — a stock-keeping unit is the same string in every language.</summary>
    [NotMultiLingual]
    public string Sku { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
