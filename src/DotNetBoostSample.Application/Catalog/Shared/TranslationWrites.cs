using DotNetBoost.MultiLingual.Core.Interfaces;

namespace DotNetBoostSample.Application.Catalog.Shared;

/// <summary>
/// Queueing per-language text alongside a write, shared by the four slices that take it:
/// creating and updating a category, and creating and updating a product.
/// </summary>
public static class TranslationWrites
{
    /// <summary>
    /// Queues one language's values per call, to be saved in the same <c>SaveChanges</c> as the
    /// slug or the SKU.
    ///
    /// <para>Uses the by-name overload because the property names arrive in a request body rather
    /// than being known at compile time. This is also why <c>Category</c> and <c>Product</c> are
    /// deliberately absent from <c>ExposeOverHttp</c>: their text is written here, in the same
    /// transaction as the row it belongs to, and a second way in would be a second answer.</para>
    /// </summary>
    public static void Apply<T>(
        ITranslationAccessor<T> accessor,
        Dictionary<string, Dictionary<string, string?>>? translations)
        where T : class
    {
        foreach (var (language, values) in translations ?? [])
            accessor.Set(language, values);
    }
}
