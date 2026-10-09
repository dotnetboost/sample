namespace DotNetBoostSample.Application.Catalog.Shared;

/// <summary>
/// The media roles the catalogue attaches under, and the largest page it will answer with.
///
/// <para>These were <c>private const</c> fields on <c>CatalogEndpoints</c>, which worked while
/// every route lived in one class. Split into eleven slices they need somewhere shared, and the
/// alternative — repeating <c>"gallery"</c> in the three slices that mention it — is how a role
/// name drifts from the one declared at startup.</para>
///
/// <para>The roles themselves are still DECLARED in <c>AddInfrastructure</c>, with
/// <c>.DeclareRoles("Product", …)</c>. These are the names this application's own code addresses
/// them by; the library's <c>GET /api/media/roles/Product</c> is what the dashboard reads.</para>
/// </summary>
public static class CatalogRoles
{
    /// <summary>The role a category's single banner image is attached under.</summary>
    public const string Banner = "banner";

    /// <summary>The role a product's single lead image is attached under.</summary>
    public const string Primary = "primary";

    /// <summary>The role a product's remaining images are attached under.</summary>
    public const string Gallery = "gallery";

    /// <summary>
    /// The largest page the catalogue will answer with, whatever is asked for.
    ///
    /// <para>A page size is a caller's suggestion — DotNetBoost.Media says exactly that of its own
    /// <c>Take</c> — and an unbounded one is how a listing endpoint becomes a way to exhaust the
    /// server's memory. <c>CatalogSettings.PageSize</c> is the default within this; it is not a way
    /// past it.</para>
    ///
    /// <para>Read in two places, and they are not duplicates of each other:
    /// <c>ListProducts.Validator</c> refuses a caller asking for more, and
    /// <c>CatalogSettingsValidator</c> refuses STORING a default larger than it — a stored page
    /// size that would be refused on every read is a setting that does not do what it says.</para>
    /// </summary>
    public const int MaxPageSize = 200;
}
