using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Metadata;
using DotNetBoost.Settings.Core.Interfaces;
using DotNetBoostSample.Api.Endpoints;
using DotNetBoostSample.Api.Integration;
using System.Text.Json;

namespace DotNetBoostSample.Api.Settings;

/// <summary>
/// What <c>GET /api/settings/branding</c> answers with: the group's stored values, its text
/// resolved into the request's language <em>beside</em> them, and its logo.
///
/// <para><b>Why a projector rather than an endpoint.</b> All three of those are facts about
/// branding, and only the first is a setting. The store name is <c>[MultiLingual]</c>, so the
/// settings row holds the default language and the other languages are translation rows; the logo
/// is a media attachment on the owner <c>("Branding", "branding")</c>, which has no table behind
/// it. DotNetBoost.Settings knows about none of that and should not: it is two other libraries'
/// data, joined here, in the one place that knows the three are about one thing.</para>
///
/// <para>This application used to answer that with a second endpoint — <c>/api/branding</c> — built
/// because there was nowhere else to put the join. <see cref="ISettingProjector{T}"/> is that
/// place, so the group's own endpoint answers completely and there is one URL for branding rather
/// than two that disagree about what it is.</para>
///
/// <para><b>What a projector may not do.</b> It shapes the READ. It is never consulted on a write,
/// and it is never applied to the <c>ETag</c> — that keeps describing the stored revision, so a
/// conditional save still protects the stored values and is not disturbed by a logo being attached
/// or a translation being written. Both are the library's rules, not this class's.</para>
///
/// <para><b>And it returns a new shape rather than mutating the group.</b> Assigning the translated
/// name onto the group would write one request's language into an object the caller owns — the
/// mistake the old endpoint carried ten lines of comment to avoid.</para>
///
/// <para><b>No property is named here.</b> The values are read off the type and the translatable
/// ones come from <c>MultiLingualMetadata</c>, so adding a property to
/// <see cref="BrandingSettings"/> — translatable or not — needs no edit in this file. Naming them
/// is what left the dashboard's per-language editor stuck on one box.</para>
/// </summary>
public sealed class BrandingProjector(
    IMultiLingualManager multiLingual,
    IMediaManager media,
    MediaText mediaText) : ISettingProjector<BrandingSettings>
{
    /// <inheritdoc/>
    public async Task<object> ProjectAsync(BrandingSettings group, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        var stored = await multiLingual.For(group).GetAllAsync(ct);

        // Every value the settings engine maps, by the same rule it uses: readable and writable.
        // Built rather than listed, so adding a property to BrandingSettings needs no edit here —
        // which is exactly what naming them cost the dashboard's per-language editor.
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var property in typeof(BrandingSettings).GetProperties())
        {
            if (!property.CanRead || !property.CanWrite) continue;

            values[JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = property.GetValue(group);
        }

        // The translatable ones resolved for this request, under a SEPARATE key — never over the
        // stored value above.
        //
        // This is the whole shape of the thing. The settings form loads this endpoint with the
        // reader's Accept-Language and posts the values back, and the settings row holds the
        // DEFAULT language. So replacing `storeName` with the Arabic translation would put Arabic
        // text in the box that writes the default-language row, and the next save would overwrite
        // the English name with it — silently, from a screen that never mentioned translation.
        //
        // Stored values stay stored; the resolution travels beside them. A caller rendering
        // branding reads `resolved.storeName ?? storeName`; a caller EDITING it reads `storeName`
        // and is right by default.
        var resolved = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var translatable in MultiLingualMetadata.For(typeof(BrandingSettings)))
        {
            if (InLanguage(stored, translatable.Name) is { } translated)
                resolved[JsonNamingPolicy.CamelCase.ConvertName(translatable.Name)] = translated;
        }

        values["resolved"] = resolved;

        var owner = media.For(BrandingSettings.MediaOwnerName, BrandingSettings.MediaOwnerId);
        var logo  = await owner.GetByRoleAsync(BrandingSettings.LogoRole, ct);

        // The media, which is not a setting and is deliberately keyed outside the group's own
        // properties — GroupForm draws its boxes from the schema's property list, so these two
        // travel in the same response without becoming inputs on the form.
        values["images"] = await AttachedFiles.DescribeAsync(owner, mediaText, ct);
        values["logo"]   = logo is null
            ? null
            : AttachedFiles.Describe(logo, await mediaText.GetAsync(logo.Id, ct));

        return values;
    }

    /// <summary>
    /// A property's value in the request's language, or <c>null</c> when nothing is stored for it.
    ///
    /// <para>Deliberately NOT <c>LocalizeAsync</c>: outside base-language mode MultiLingual treats
    /// the translations as the only store and nulls a property it finds nothing for. Here the
    /// settings row IS the base value — the group's ETag covers it — so a missing translation must
    /// leave it alone rather than erase it. Hence the <c>??</c> at each call site.</para>
    /// </summary>
    private string? InLanguage(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> stored, string propertyName) =>
        stored.TryGetValue(multiLingual.CurrentLanguage.Code, out var inLanguage)
        && inLanguage.TryGetValue(propertyName, out var translated)
        && !string.IsNullOrWhiteSpace(translated)
            ? translated
            : null;
}
