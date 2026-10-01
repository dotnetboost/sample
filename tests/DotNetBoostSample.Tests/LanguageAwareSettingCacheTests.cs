using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Models;
using DotNetBoostSample.Api.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DotNetBoostSample.Tests;

/// <summary>
/// The cache has to partition by language and evict across all of them. Neither is observable from
/// the API while every language still returns the same values, so it is pinned here instead —
/// otherwise a suffix that silently did nothing would look exactly like one that worked.
/// </summary>
public class LanguageAwareSettingCacheTests
{
    private const string Key = "dnb:setting:branding";

    [Fact]
    public void Each_language_gets_its_own_entry()
    {
        var language = new MutableLanguage("en");
        var cache = Build(language);

        cache.Set(Key, "english value", TimeSpan.FromMinutes(5));

        // Same key, different language: a miss, because the entry belongs to en.
        language.Code = "ar";
        Assert.False(cache.TryGetValue<string>(Key, out _));

        // And back again: still there.
        language.Code = "en";
        Assert.True(cache.TryGetValue<string>(Key, out var value));
        Assert.Equal("english value", value);
    }

    [Fact]
    public void Languages_do_not_overwrite_each_other()
    {
        var language = new MutableLanguage("en");
        var cache = Build(language);

        cache.Set(Key, "english value", TimeSpan.FromMinutes(5));

        language.Code = "ar";
        cache.Set(Key, "arabic value", TimeSpan.FromMinutes(5));

        language.Code = "en";
        Assert.True(cache.TryGetValue<string>(Key, out var english));
        Assert.Equal("english value", english);
    }

    [Fact]
    public void Removing_in_one_language_evicts_every_language()
    {
        var language = new MutableLanguage("en");
        var cache = Build(language);

        cache.Set(Key, "english value", TimeSpan.FromMinutes(5));

        language.Code = "ar";
        cache.Set(Key, "arabic value", TimeSpan.FromMinutes(5));

        language.Code = "fr";
        cache.Set(Key, "french value", TimeSpan.FromMinutes(5));

        // SettingManager removes one key after a write. Every language's entry must go with it,
        // or a write in English leaves Arabic serving the values from before it.
        language.Code = "en";
        cache.Remove(Key);

        foreach (var code in new[] { "en", "ar", "fr" })
        {
            language.Code = code;
            Assert.False(cache.TryGetValue<string>(Key, out _), $"{code} survived the eviction.");
        }
    }

    [Fact]
    public void Without_a_request_the_key_is_left_alone()
    {
        // A seeder or a background job has no scope and no resolved language. The key must then be
        // exactly what the library would have used on its own, so the two paths share an entry.
        var cache = new LanguageAwareSettingCache(
            new MemoryCache(new MemoryCacheOptions()),
            new HttpContextAccessor());

        cache.Set(Key, "value", TimeSpan.FromMinutes(5));

        Assert.True(cache.TryGetValue<string>(Key, out var value));
        Assert.Equal("value", value);
    }

    [Fact]
    public void An_unresolved_language_is_treated_as_no_language()
    {
        var language = new MutableLanguage("en") { IsLanguageResolved = false };
        var cache = Build(language);

        cache.Set(Key, "value", TimeSpan.FromMinutes(5));

        // Resolving one afterwards must not find the unsuffixed entry under a suffixed key.
        language.IsLanguageResolved = true;
        Assert.False(cache.TryGetValue<string>(Key, out _));
    }

    private static LanguageAwareSettingCache Build(IMultiLingualManager manager)
    {
        var services = new ServiceCollection();
        services.AddSingleton(manager);

        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() }
        };

        return new LanguageAwareSettingCache(new MemoryCache(new MemoryCacheOptions()), accessor);
    }

    /// <summary>
    /// The two members <see cref="LanguageAwareSettingCache"/> actually reads. Everything else on
    /// the interface throws, so a test that started depending on more would say so rather than
    /// quietly pass against a half-built fake.
    /// </summary>
    private sealed class MutableLanguage(string code) : IMultiLingualManager
    {
        public string Code { get; set; } = code;

        public bool IsLanguageResolved { get; set; } = true;

        public LanguageInfo CurrentLanguage => new(Code, Code, TextDirection.Ltr, false, true);

        public LanguageInfo DefaultLanguage => throw new NotSupportedException();
        public bool WasRequestedLanguageHonoured => throw new NotSupportedException();
        public ILanguageAccessor Languages => throw new NotSupportedException();

        public Task<bool> UseLanguageAsync(string? languageCode, CancellationToken ct = default)
            => throw new NotSupportedException();

        public ITranslationAccessor<T> For<T>(T entity) where T : class
            => throw new NotSupportedException();

        public Task LocalizeAsync(IEnumerable<object> entities, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task LocalizeAsync(IEnumerable<object> entities, string languageCode, CancellationToken ct = default)
            => throw new NotSupportedException();

        public void Localize(IEnumerable<object> entities) => throw new NotSupportedException();

        public void Localize(IEnumerable<object> entities, string languageCode) => throw new NotSupportedException();
    }
}
