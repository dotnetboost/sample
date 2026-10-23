using DotNetBoost.Auditing.Core.Attributes;
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

    /// <summary>
    /// Translatable.
    ///
    /// <para><c>[AuditIgnore]</c> is the second library's attribute on the first library's
    /// property, and it is here because of what the first one does: this value depends on the
    /// language, and an audit entry has no language. <c>Name: "Desk" → "Writing desk"</c> would be
    /// a true sentence about one of four languages and silent about which — and in the mode this
    /// sample ships in there is no column here at all, so the value a diff would compare is
    /// whatever was hydrated for the language the request happened to be read in.</para>
    ///
    /// <para>Said with the engine's own attribute rather than filtered in the application, so that
    /// <em>every</em> way of recording honours it — including <c>GET /api/audit/entities/Product</c>,
    /// which reports the rule as <c>Ignore</c> rather than claiming a property the trail never
    /// carries. Translated text has its own history to be given one day, in the Translations table,
    /// with a language on each row.</para>
    /// </summary>
    [AuditIgnore]
    public string Name { get; set; } = string.Empty;

    /// <summary>Translatable, and out of the trail for the same reason as <see cref="Name"/>.</summary>
    [AuditIgnore]
    public string Description { get; set; } = string.Empty;

    /// <summary>NOT translatable — a stock-keeping unit is the same string in every language.</summary>
    [NotMultiLingual]
    public string Sku { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
