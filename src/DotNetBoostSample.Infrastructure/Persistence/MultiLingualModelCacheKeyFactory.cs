using DotNetBoost.MultiLingual.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetBoostSample.Infrastructure.Persistence;

/// <summary>
/// Puts <c>MultiLingualOptions.BaseLanguage</c> into EF Core's model cache key.
///
/// <para><b>Why this is needed at all.</b> EF builds a model once and caches it per DbContext
/// TYPE, because <c>OnModelCreating</c> is assumed to produce the same model every time.
/// <c>ApplyMultiLingual(this)</c> breaks that assumption in one specific way: it reads
/// <c>BaseLanguage</c> out of the application's services, and the answer decides whether
/// <c>Product.Name</c> is a column or is stripped off the model altogether. Two instances of this
/// application in ONE process with different answers would therefore share whichever model was
/// built first.</para>
///
/// <para><b>What that looks like without this class.</b> Not silence — the library checks the
/// model against the engine's configuration on the first save and refuses: "BaseLanguage is 'en',
/// so 'Category.Name' must keep its column — but 'AppDbContext' does not map it." Which is the
/// right failure, and still a failure: the mode is configuration, so the two can legitimately
/// differ between two hosts in one process. <c>BaseLanguageTests</c> is where that stopped being
/// hypothetical — it boots the sample a second time, the other way, beside the suite that boots
/// it the first.</para>
///
/// <para><b>How it reaches the option.</b> A service put in with <c>ReplaceService</c> is built by
/// EF's own internal container, which knows nothing about this application's. The application's
/// provider hangs off the context's options, which is also how <c>ApplyMultiLingual(this)</c>
/// finds the same object — so this asks the same question the same way rather than being told the
/// answer twice.</para>
///
/// <para>Outside base-language mode the extra key component is <c>null</c> and the behaviour is
/// EF's own.</para>
/// </summary>
public sealed class MultiLingualModelCacheKeyFactory : IModelCacheKeyFactory
{
    /// <inheritdoc/>
    public object Create(DbContext context, bool designTime) =>
        (context.GetType(), BaseLanguageOf(context), designTime);

    /// <summary>
    /// The configured base language, or <c>null</c> — including for a design-time context built
    /// without the application's services, which is the one case where there is nothing to ask.
    /// </summary>
    private static string? BaseLanguageOf(DbContext context) =>
        context.GetService<IDbContextOptions>()
               .FindExtension<CoreOptionsExtension>()
               ?.ApplicationServiceProvider
               ?.GetService<MultiLingualOptions>()
               ?.BaseLanguage;
}
