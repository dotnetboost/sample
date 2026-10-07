using DotNetBoost.Settings.Core.Attributes;
using DotNetBoostSample.Api.Endpoints;
using FluentValidation;

namespace DotNetBoostSample.Api.Settings;

/// <summary>
/// A second group, to show more than one on the settings screen: how the catalogue is presented.
///
/// <para>Its rules sit below it, in this file, for the reason <c>BrandingSettings.cs</c> gives at
/// the top: a group and the bounds on its values are one subject.</para>
/// </summary>
[SettingGroup("catalog")]
public class CatalogSettings
{
    /// <summary>Products returned per page.</summary>
    public int PageSize { get; set; } = 12;

    /// <summary>Currency symbol shown next to prices.</summary>
    public string CurrencySymbol { get; set; } = "$";
}

/// <summary>
/// Rules for the catalogue group. <see cref="BrandingSettingsValidator"/> explains why these are
/// published as well as enforced.
/// </summary>
public sealed class CatalogSettingsValidator : AbstractValidator<CatalogSettings>
{
    /// <summary>Creates the validator.</summary>
    public CatalogSettingsValidator()
    {
        // The upper bound is the endpoint's own cap, not a second opinion about it: a page size
        // larger than CatalogEndpoints.MaxPageSize would be silently clamped on every read, so
        // storing one is a setting that does not do what it says.
        RuleFor(c => c.PageSize)
            .InclusiveBetween(1, CatalogEndpoints.MaxPageSize);

        // A glyph, not a currency code — see formatPrice in the dashboard, which places it with
        // Intl and needs nothing more than the symbol itself.
        RuleFor(c => c.CurrencySymbol)
            .NotEmpty()
            .MaximumLength(3);
    }
}
