using DotNetBoost.Media.Core.Attributes;
using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Attributes;
using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Metadata;
using DotNetBoost.Settings.Core;
using DotNetBoost.Settings.Core.Attributes;
using DotNetBoost.Settings.Core.Interfaces;
using DotNetBoostSample.Application.Catalog.Shared;
using DotNetBoostSample.Application.Composition;
using FluentValidation;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetBoostSample.Application.Settings;

// One settings group per file, with everything that names it: the class, its validator, and its
// projector with the response type that projector publishes.

/// <summary>
/// The site's branding: runtime-editable text, and a logo attached through the media library.
///
/// <para><b>The logo is an attachment, not a property.</b> A file id stored in a setting is
/// invisible to DotNetBoost.Media — the file is used by nothing and deletes without a warning — and
/// keeping the id as well as an attachment gives two records that drift. So the attachment is the
/// only record, at the cost of the logo being outside this group's ETag and audit trail. The
/// README's "Why the logo is not a <c>Guid</c>" has the long version.</para>
///
/// <para><b>The owner has no row.</b> A media owner is two strings the library never resolves,
/// which is what lets a settings group with no table own files.</para>
/// </summary>
[SettingGroup("branding")]
// Pins the stored owner name, so renaming this class cannot orphan the attachments.
[MediaEntity(MediaOwnerName)]
// The same name on the MultiLingual side, so one subject carries the logo and StoreName's
// translations. Nothing in either library notices if the two disagree; BrandingOwnerTests does.
[MultiLingualEntity(MediaOwnerName)]
// Read by DeclareRoles<BrandingSettings>().
[MediaRole(LogoRole)]
public class BrandingSettings
{
    /// <summary>The owner name these attachments are filed under.</summary>
    public const string MediaOwnerName = "Branding";

    /// <summary>
    /// The owner id. Degenerate, because there is only ever one branding — like <c>Id = 1</c> on a
    /// single-row config table — but the media library needs both halves of an owner address.
    /// </summary>
    public const string MediaOwnerId = "branding";

    /// <summary>The role the logo is attached under. Exactly one per owner.</summary>
    public const string LogoRole = "logo";

    /// <summary>
    /// The id MultiLingual addresses this group by; a translation row needs an id as well as a name.
    ///
    /// <para><b>Get-only on purpose.</b> The settings library maps only read-write properties, so
    /// this never becomes a setting row, while MultiLingual's convention accessor finds it by name
    /// and only reads it. <c>[JsonIgnore]</c> because System.Text.Json does serialise read-only
    /// properties, and the dashboard's generated form would grow an "Id" box for it.</para>
    /// </summary>
    [JsonIgnore]
    public string Id => MediaOwnerId;

    /// <summary>Shown in the dashboard header.</summary>
    [MultiLingual]
    public string StoreName { get; set; } = "DotNetBoost Sample Store";

    /// <summary>Accent colour, as a CSS hex value.</summary>
    public string AccentColor { get; set; } = "#512BD4";

    /// <summary>
    /// How large the logo is drawn in the dashboard header, in pixels — a setting so it can be
    /// nudged after seeing the logo in place, with no redeploy. Bounded by
    /// <see cref="BrandingSettingsValidator"/>, which the schema endpoint publishes.
    /// </summary>
    public int HeaderLogoSize { get; set; } = 28;
}

/// <summary>
/// Rules for the branding group, enforced on every write and <b>published</b> by
/// <c>GET /api/settings/branding/schema</c>: DotNetBoost.Settings.FluentValidation turns them into
/// <c>min</c>, <c>max</c>, <c>maxLength</c> and <c>pattern</c>, and the dashboard's settings form
/// draws its controls from those. One rule, two readers, and the server decides.
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

        // What the dashboard's colour input produces and the header's inline style expects.
        RuleFor(b => b.AccentColor)
            .NotEmpty()
            .Matches("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")
            .WithMessage("'{PropertyName}' must be a CSS hex colour, for example #512BD4.");

        RuleFor(b => b.HeaderLogoSize)
            .InclusiveBetween(MinLogoSize, MaxLogoSize);
    }
}

/// <summary>
/// What <c>GET /api/settings/branding</c> answers with, and the type the OpenAPI document
/// describes. <see cref="BrandingProjector"/> declares it through
/// <c>ISettingProjector&lt;BrandingSettings, BrandingResponse&gt;</c>, so the compiler keeps the
/// published type and the returned type the same one.
///
/// <para><b>It derives from the group</b>, so every stored property is in the response and the
/// schema with no edit here. That is safe in both libraries: <c>[SettingGroup]</c> is not
/// inherited, so this is not a second group claiming <c>branding</c>; Media reads
/// <c>[MediaRole]</c> without inheritance, so the <c>logo</c> role is not filed twice; and the
/// inherited <c>[JsonIgnore]</c> keeps <c>Id</c> off the wire.</para>
///
/// <para><b>Only the read.</b> <c>POST /api/settings/branding</c> still takes
/// <see cref="BrandingSettings"/>. Translations are written to
/// <c>PUT /api/translations/Branding/branding</c> and the logo to
/// <c>POST /api/media/{id}/attachments</c>.</para>
/// </summary>
public sealed class BrandingResponse : BrandingSettings
{
    /// <summary>
    /// The group's <c>[MultiLingual]</c> properties in the request's language, keyed by JSON name,
    /// for those a translation exists for. Beside the stored values rather than over them: a caller
    /// rendering branding reads <c>resolved.storeName ?? storeName</c>; a caller editing it reads
    /// <c>storeName</c>, which is the default-language row it will write back.
    /// </summary>
    public IReadOnlyDictionary<string, string> Resolved { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Everything attached to the owner <c>("Branding", "branding")</c>, with roles — the same shape
    /// a product's images come back in, so the dashboard draws both with one component.
    /// </summary>
    public IReadOnlyList<AttachedFileInRole> Images { get; init; } = [];

    /// <summary>The file in the <c>logo</c> role, or <c>null</c>.</summary>
    public AttachedFile? Logo { get; init; }

    /// <summary>
    /// The two strings <see cref="Images"/> are filed under, so a client knows where to attach.
    /// Nothing else tells it: the group's class name is exactly what the owner is not, and
    /// <c>GET /api/media/roles/BrandingSettings</c> answers an empty <c>200</c>, so a client
    /// guessing would show no media with nothing failing.
    /// </summary>
    public MediaOwner Owner { get; init; } =
        new(BrandingSettings.MediaOwnerName, BrandingSettings.MediaOwnerId);
}

/// <summary>
/// An owner as DotNetBoost.Media addresses one: two strings, and no row behind them.
/// </summary>
/// <param name="EntityName">The name roles and attachments are filed under.</param>
/// <param name="EntityId">Which one of them, degenerate where there can only be one.</param>
public sealed record MediaOwner(string EntityName, string EntityId);

/// <summary>
/// What <c>GET /api/settings/branding</c> answers with: the group's stored values, its text in the
/// request's language <em>beside</em> them, and its logo.
///
/// <para><b>Why a projector.</b> Only the first of those is a setting. The other languages are
/// MultiLingual's rows and the logo is a Media attachment; DotNetBoost.Settings knows about neither
/// and should not. <see cref="ISettingProjector{T}"/> is where the group's own endpoint lets this
/// application join them, so there is one URL for branding.</para>
///
/// <para><b>What it may not do</b> — the library's rules, not this class's: it shapes only the read,
/// and the <c>ETag</c> keeps describing the stored revision, so attaching a logo or writing a
/// translation does not disturb a conditional save. It returns a new object rather than writing one
/// request's language onto the group the caller owns.</para>
///
/// <para><b>No property is named here.</b> Values are copied by the settings engine's own rule and
/// translatable properties come from <c>MultiLingualMetadata</c>, so adding a property to
/// <see cref="BrandingSettings"/> needs no edit below.</para>
/// </summary>
public sealed class BrandingProjector(
    IMultiLingualManager multiLingual,
    IMediaManager media,
    MediaText mediaText) : ISettingProjector<BrandingSettings, BrandingResponse>
{
    /// <inheritdoc/>
    public async Task<BrandingResponse> ProjectAsync(BrandingSettings group, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        var stored = await multiLingual.For(group).GetAllAsync(ct);

        // Under a separate key, never over the stored value: the settings form posts these values
        // back to the default-language row, and an Arabic name in that box would overwrite the
        // English one on the next save.
        //
        // Camel-cased by hand because these are dictionary keys, which the Web JSON defaults leave
        // alone — `resolved` would otherwise answer `StoreName` beside `storeName`.
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var translatable in MultiLingualMetadata.For(typeof(BrandingSettings)))
        {
            if (InLanguage(stored, translatable.Name) is { } translated)
                resolved[JsonNamingPolicy.CamelCase.ConvertName(translatable.Name)] = translated;
        }

        var owner = media.For(BrandingSettings.MediaOwnerName, BrandingSettings.MediaOwnerId);
        var logo  = await owner.GetByRoleAsync(BrandingSettings.LogoRole, ct);

        // Not among the group's own properties, so the form, which draws its boxes from the
        // schema, does not turn them into inputs.
        var response = new BrandingResponse
        {
            Resolved = resolved,
            Images   = await AttachedFiles.DescribeAsync(owner, mediaText, ct),
            Logo     = logo is null
                ? null
                : AttachedFiles.Describe(logo, await mediaText.GetAsync(logo.Id, ct))
        };

        // The rule SettingManager builds its own map from, so this cannot copy a property the
        // engine ignores or miss one it stores.
        foreach (var property in SettingSchema.StorableProperties(typeof(BrandingSettings)))
            property.SetValue(response, property.GetValue(group));

        return response;
    }

    /// <summary>
    /// A property's value in the request's language, or <c>null</c> when nothing is stored for it.
    ///
    /// <para>Deliberately NOT <c>LocalizeAsync</c>: outside base-language mode MultiLingual treats
    /// the translations as the only store and nulls a property it finds nothing for. Here the
    /// settings row IS the base value, so a missing translation must leave it alone.</para>
    /// </summary>
    private string? InLanguage(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> stored, string propertyName) =>
        stored.TryGetValue(multiLingual.CurrentLanguage.Code, out var inLanguage)
        && inLanguage.TryGetValue(propertyName, out var translated)
        && !string.IsNullOrWhiteSpace(translated)
            ? translated
            : null;
}
