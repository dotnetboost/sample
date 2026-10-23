using DotNetBoost.Auditing.Core.Attributes;
using DotNetBoost.MultiLingual.Core.Attributes;

namespace DotNetBoostSample.Domain.Catalog;

/// <summary>
/// One of the sample's own two entities — nothing in any DotNetBoost library knows this type
/// exists.
/// <para>
/// <c>[MultiLingual]</c> at class level makes every public string property translatable:
/// <see cref="Name"/> and <see cref="Description"/> are read and written per language rather than
/// once. Where they are STORED is the engine's configuration rather than this class's business —
/// as rows in the translations table, one per language, unless a base language is configured, in
/// which case that one language keeps a column here and the rest are still rows. Either way
/// nothing below changes, which is the point of the attribute.
/// </para>
/// </summary>
[MultiLingual]
public class Category
{
    public int Id { get; set; }

    /// <summary>
    /// Translatable. Read in the request's language, with fallback to the default.
    ///
    /// <para><c>[AuditIgnore]</c> for the reason spelled out on <see cref="Product.Name"/>: a trail
    /// has no language, and this value has nothing else.</para>
    /// </summary>
    [AuditIgnore]
    public string Name { get; set; } = string.Empty;

    /// <summary>Translatable, and out of the trail for the same reason as <see cref="Name"/>.</summary>
    [AuditIgnore]
    public string Description { get; set; } = string.Empty;

    /// <summary>NOT translatable — a stable identifier used in URLs, the same in every language.</summary>
    [NotMultiLingual]
    public string Slug { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Product> Products { get; set; } = [];
}
