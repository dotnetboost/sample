using DotNetBoost.Settings.Core.Attributes;

namespace DotNetBoostSample.Api.Settings;

/// <summary>
/// DotNetBoost.Settings and DotNetBoost.Media composed with no mechanism at all: a setting whose
/// value happens to be a media file's id. The settings library stores a Guid as it would store
/// any other value, and the application resolves it through <c>IMediaManager</c> on read.
/// <para>
/// Editable at runtime over <c>GET/POST /api/settings/branding</c> — change the logo without a
/// redeploy.
/// </para>
/// </summary>
[SettingGroup("branding")]
public class BrandingSettings
{
    /// <summary>
    /// The uploaded logo, or <see cref="Guid.Empty"/> for none. Just a value to the settings
    /// library; meaningful only to the code that passes it to the media library.
    /// </summary>
    public Guid LogoFileId { get; set; }

    /// <summary>Shown in the dashboard header.</summary>
    public string StoreName { get; set; } = "DotNetBoost Sample Store";

    /// <summary>Accent colour, as a CSS hex value.</summary>
    public string AccentColor { get; set; } = "#512BD4";
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
