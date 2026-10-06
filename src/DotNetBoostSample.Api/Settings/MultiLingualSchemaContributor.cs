using DotNetBoost.MultiLingual.Core.Metadata;
using DotNetBoost.Settings.Core.Interfaces;

namespace DotNetBoostSample.Api.Settings;

/// <summary>
/// Publishes <c>translated</c> on a settings property carrying <c>[MultiLingual]</c>, so
/// <c>GET /api/settings/{route}/schema</c> says which boxes edit a value that has other languages.
///
/// <para><b>Why this is the application's job.</b> DotNetBoost.Settings has never heard of
/// <c>[MultiLingual]</c> — it is DotNetBoost.MultiLingual's attribute, and the two libraries do not
/// reference each other. So the settings engine cannot publish it, and before this the dashboard
/// carried the flag itself: a hand-written <c>storeName: { translated: true }</c> in
/// <c>clients/dashboard/app/utils/settings.ts</c>, which nothing kept in step with the C# class.
/// <see cref="ISettingSchemaContributor"/> is the port for exactly this — a constraint the
/// settings library cannot know about — and this class is the sample's whole subject in miniature:
/// two independent libraries, joined by one small piece of application code.</para>
///
/// <para><b>It composes rather than replaces.</b> DotNetBoost.Settings.FluentValidation registers
/// a contributor too, over the rules in <see cref="BrandingSettingsValidator"/>. The schema
/// endpoint merges every applicable contributor, so <c>storeName</c> comes back with
/// <c>required</c> and <c>maxLength</c> from those rules <em>and</em> <c>translated</c> from here.
/// Until preview.2 it was first-match-wins, and registering this would have silently deleted the
/// bounds.</para>
///
/// <para><b>The rule is asked, not reimplemented.</b> <c>[MultiLingual]</c> may sit on a property
/// or on the class, and the library resolves which properties that makes translatable.
/// <see cref="MultiLingualMetadata.For(Type)"/> is that resolution, so this cannot drift from what
/// the translation store actually writes.</para>
/// </summary>
public sealed class MultiLingualSchemaContributor : ISettingSchemaContributor
{
    /// <summary>The constraint name, matching the field hint the dashboard reads.</summary>
    private const string Translated = "translated";

    /// <inheritdoc/>
    public bool CanDescribe(Type type) => MultiLingualMetadata.IsMultiLingual(type);

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> Describe(Type type) =>
        MultiLingualMetadata.For(type).ToDictionary(
            property => property.Name,
            property => (IReadOnlyDictionary<string, object?>)
                new Dictionary<string, object?> { [Translated] = true },
            StringComparer.Ordinal);
}
