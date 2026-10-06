using DotNetBoostSample.Api.Endpoints;
using FluentValidation;

namespace DotNetBoostSample.Api.Settings;

/// <summary>
/// Rules for the branding group, enforced on every write and <b>published</b> by
/// <c>GET /api/settings/branding/schema</c>.
///
/// <para><b>Why they are worth having twice over.</b> A validator refuses a bad write — that is the
/// obvious half. The other half is that DotNetBoost.Settings.FluentValidation registers an
/// <c>ISettingSchemaContributor</c> over these rules, so the schema endpoint answers with
/// <c>min</c>, <c>max</c>, <c>maxLength</c> and the rest. The dashboard's settings form reads those
/// and draws its controls from them.</para>
///
/// <para>Before this, the bounds lived in the client — <c>clients/dashboard/app/utils/settings.ts</c>
/// hardcoded <c>headerLogoSize: { min: 16, max: 64 }</c>, a range that appeared nowhere in C# and
/// which the API would happily have ignored. One rule, two readers, and the server is the one that
/// decides.</para>
/// </summary>
public sealed class BrandingSettingsValidator : AbstractValidator<BrandingSettings>
{
    /// <summary>The smallest and largest the header logo may be drawn, in pixels.</summary>
    public const int MinLogoSize = 16;

    /// <inheritdoc cref="MinLogoSize"/>
    public const int MaxLogoSize = 64;

    /// <summary>Creates the validator.</summary>
    public BrandingSettingsValidator()
    {
        RuleFor(b => b.StoreName)
            .NotEmpty()
            .MaximumLength(120);

        // A CSS hex colour, which is what the dashboard's colour input produces and what the
        // header's inline style expects. Published as `pattern`, so a generated form can refuse
        // the value before sending it.
        RuleFor(b => b.AccentColor)
            .NotEmpty()
            .Matches("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")
            .WithMessage("'{PropertyName}' must be a CSS hex colour, for example #512BD4.");

        // The range the dashboard used to invent for itself. Enforced here, so a careless value
        // cannot reach the header at all rather than being clamped at render time.
        RuleFor(b => b.HeaderLogoSize)
            .InclusiveBetween(MinLogoSize, MaxLogoSize);
    }
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
