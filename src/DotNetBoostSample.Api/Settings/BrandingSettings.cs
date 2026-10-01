using DotNetBoost.Media.Core.Attributes;
using DotNetBoost.MultiLingual.Core.Attributes;
using DotNetBoost.Settings.Core.Attributes;

namespace DotNetBoostSample.Api.Settings;

/// <summary>
/// The site's branding: runtime-editable text, and — through the media library rather than through
/// this class — a logo.
///
/// <para><b>Why the logo is not a property here.</b> It was, as a <c>Guid LogoFileId</c>, and that is
/// a perfectly ordinary way to reference a file. But a file referenced only by a setting's value is
/// invisible to DotNetBoost.Media: it appears in no entity's attachments, its details panel says it
/// is used by nothing, and deleting it warns about nothing. Attaching it as well fixed the reading
/// but left two records of one fact, which drifted the moment anyone detached the file from the
/// media page — the attachment went, the setting did not.</para>
///
/// <para>So the attachment is now the only record. There is nothing to keep in step, and detaching
/// the logo means the site has no logo, because that is the same statement.</para>
///
/// <para><b>What that costs.</b> The logo is no longer part of this settings group, so it is absent
/// from <c>GET /api/settings/branding</c>, from the group's ETag, and from its audit trail. Those
/// are real losses, and they are what storing an id in a setting buys. They are given up here in
/// exchange for a single source of truth.</para>
///
/// <para><b>The owner.</b> Branding has no row anywhere — it is one group of setting rows, and there
/// can only ever be one of it. The media library does not care: an owner is two strings, and it
/// never tries to resolve them to anything. That is what lets a thing with no table own files, and
/// it is the sharpest demonstration of the attachment model in this sample — a foreign key could
/// not do this.</para>
/// </summary>
[SettingGroup("branding")]
// Pins the name the attachments are stored under, so renaming this class cannot orphan them.
// Without it the stored name would follow the class — "BrandingSettings" today, whatever it is
// called tomorrow, and every existing row addressing the old one.
[MediaEntity(MediaOwnerName)]
// Declared on the class rather than in Program.cs, so the roles live next to the thing that has
// them: open this file and you can see what branding holds. Read by DeclareRoles<BrandingSettings>().
// A second, multi-valued role is one more line — [MediaRole("banners", Multiple = true)].
[MediaRole(LogoRole)]
public class BrandingSettings
{
    /// <summary>The owner name these attachments are filed under.</summary>
    public const string MediaOwnerName = "Branding";

    /// <summary>
    /// The owner id. Degenerate, because there is only ever one branding — the same way a config
    /// table with a single row conventionally uses <c>Id = 1</c>. It is the group's own route from
    /// <see cref="SettingGroupAttribute"/> rather than a new invention, and it has to be
    /// <em>something</em>: the media library requires both halves of an owner address, which is
    /// what lets one table serve every owner with no column per owner type.
    /// </summary>
    public const string MediaOwnerId = "branding";

    /// <summary>The role the logo is attached under. Exactly one per owner.</summary>
    public const string LogoRole = "logo";

    /// <summary>
    /// Shown in the dashboard header.
    /// <para>
    /// The attribute goes BELOW the doc comment: a <c>///</c> block between an attribute and its
    /// member is CS1587, and is dropped from IntelliSense even where documentation generation is
    /// off and the warning never fires.
    /// </para>
    /// </summary>
    [MultiLingual]
    public string StoreName { get; set; } = "DotNetBoost Sample Store";

    /// <summary>Accent colour, as a CSS hex value.</summary>
    public string AccentColor { get; set; } = "#512BD4";

    /// <summary>
    /// How large the logo is drawn in the dashboard header, in pixels.
    /// <para>
    /// A setting rather than a constant in the client because it is exactly the kind of thing
    /// someone wants to nudge after seeing their own logo in place — which is what this library is
    /// for: change it, reload, done, with no redeploy. The logo it sizes is not stored here, and
    /// the two are independent: this is a number whether or not a logo is attached.
    /// </para>
    /// <para>
    /// The API accepts any <c>int</c>; the dashboard offers a sensible range and clamps what it
    /// renders, so a careless value cannot break the layout. Enforcing it server-side would mean
    /// adding <c>DotNetBoost.Settings.FluentValidation</c>, which this sample does not install.
    /// </para>
    /// </summary>
    public int HeaderLogoSize { get; set; } = 28;
}

/// <summary>
/// A second group, to show more than one on the settings screen: how the catalogue is presented.
/// </summary>
[SettingGroup("catalog")]
public class CatalogSettings
{
    /// <summary>Products returned per page.</summary>
    public int PageSize { get; set; } = 12;

    /// <summary>Whether products with no image are listed at all.</summary>
    public bool ShowProductsWithoutImages { get; set; } = true;

    /// <summary>Currency symbol shown next to prices.</summary>
    public string CurrencySymbol { get; set; } = "$";
}
