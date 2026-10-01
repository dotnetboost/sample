using System.Collections.Concurrent;
using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoost.Settings.Core.Interfaces;
using DotNetBoost.Settings.Core.Services;
using Microsoft.Extensions.Caching.Memory;

namespace DotNetBoostSample.Api.Settings;

/// <summary>
/// The settings cache, keyed by the request's language as well as the group.
///
/// <para><b>Why.</b> <c>SettingManager</c> caches a whole group under <c>dnb:setting:{group}</c>.
/// The moment any value in a group varies by language, that key is wrong: whoever reads first wins,
/// and everyone else gets their language's values out of someone else's cache entry. This appends
/// the language, so each one gets its own entry.</para>
///
/// <para><b>Eviction has to be wider than the read.</b> <c>SettingManager</c> removes
/// <c>dnb:setting:{group}</c> after a write — one key, the current language's. Every other
/// language's entry would survive with the old values. So every suffix this cache has ever issued
/// is remembered, and a removal clears the group in all of them. Over-evicting costs a reload;
/// under-evicting serves a stale value until the entry expires.</para>
///
/// <para><b>Reading a scoped language from a singleton.</b> The cache is registered as a singleton
/// and <c>IMultiLingualManager</c> is scoped, so the manager is resolved per call from the current
/// request's own service provider. Outside a request — a background job, a seeder — there is no
/// scope and no resolved language, and the key stays unsuffixed, which is the same key the library
/// would have used on its own.</para>
///
/// <para>Registered with <c>.UseCustomCache&lt;LanguageAwareSettingCache&gt;()</c>.</para>
/// </summary>
public sealed class LanguageAwareSettingCache(
    IMemoryCache memoryCache,
    IHttpContextAccessor httpContextAccessor) : ISettingCache
{
    private readonly SettingCache _inner = new(memoryCache);

    /// <summary>
    /// Every language suffix handed out so far, so <see cref="Remove"/> can clear all of them.
    /// Bounded by the number of configured languages, so it does not grow without limit.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _suffixes = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public bool TryGetValue<T>(string key, out T? value) => _inner.TryGetValue(Scoped(key), out value);

    /// <inheritdoc/>
    public void Set<T>(string key, T value, TimeSpan duration) => _inner.Set(Scoped(key), value, duration);

    /// <summary>
    /// Evicts the group in every language, not only the caller's.
    /// <para>
    /// The unsuffixed key goes too: it is what gets written outside a request, and a write made in
    /// one language must not leave a background job reading the values from before it.
    /// </para>
    /// </summary>
    public void Remove(string key)
    {
        _inner.Remove(key);

        foreach (var suffix in _suffixes.Keys)
        {
            _inner.Remove(key + suffix);
        }
    }

    /// <summary>
    /// <paramref name="key"/> with the current language appended, or unchanged when no language has
    /// been resolved for this scope.
    /// </summary>
    private string Scoped(string key)
    {
        var services = httpContextAccessor.HttpContext?.RequestServices;

        // No request, so no scope to read a language from — a seeder or a background job.
        if (services is null) return key;

        var multiLingual = services.GetService<IMultiLingualManager>();

        // IsLanguageResolved first: CurrentLanguage throws when nothing has resolved one, which
        // happens on any request that reaches an endpoint before UseMultiLingual has run.
        if (multiLingual is null || !multiLingual.IsLanguageResolved) return key;

        var suffix = ":lang:" + multiLingual.CurrentLanguage.Code;
        _suffixes.TryAdd(suffix, 0);

        return key + suffix;
    }
}
