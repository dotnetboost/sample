namespace DotNetBoostSample.Application.Localization;

/// <summary>
/// The marker type the API's own messages are injected under: <c>IStringLocalizer&lt;ApiMessages&gt;</c>.
///
/// <para>It names the consumer and nothing else. Every localizer in the application shares the same
/// <c>Resources/*.json</c> files, so this is not a per-type resource the way <c>.resx</c> would be —
/// one type for the whole API keeps the keys in one namespace rather than scattering them behind
/// whichever class happened to need them.</para>
///
/// <para><b>What makes this different from <c>AddLocalization()</c>.</b> The standard localizer
/// reads <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>. This one reads
/// <c>IMultiLingualManager.CurrentLanguage</c> — the same language the EF Core interceptors
/// translated the row with, resolved from the same <c>Accept-Language</c>, falling back the same
/// way. So a refused request and the content it was about cannot disagree about what language the
/// caller reads.</para>
/// </summary>
public sealed class ApiMessages;
