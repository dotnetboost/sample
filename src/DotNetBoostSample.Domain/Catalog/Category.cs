using DotNetBoost.MultiLingual.Core.Attributes;

namespace DotNetBoostSample.Domain.Catalog;

/// <summary>
/// One of the sample's own two entities — nothing in any DotNetBoost library knows this type
/// exists.
/// <para>
/// <c>[MultiLingual]</c> at class level makes every public string property translatable:
/// <see cref="Name"/> and <see cref="Description"/> are not stored in the Categories table at
/// all, but as rows in the translations table, one per language. The EF Core provider removes
/// them from the model, so no migration here mentions them.
/// </para>
/// </summary>
[MultiLingual]
public class Category
{
    public int Id { get; set; }

    /// <summary>Translatable. Read in the request's language, with fallback to the default.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Translatable.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>NOT translatable — a stable identifier used in URLs, the same in every language.</summary>
    [NotMultiLingual]
    public string Slug { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Product> Products { get; set; } = [];
}
