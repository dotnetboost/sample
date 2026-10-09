using DotNetBoost.Media.Core.Attributes;
using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Attributes;
using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Metadata;
using DotNetBoost.Settings.Core;
using DotNetBoost.Settings.Core.Attributes;
using DotNetBoost.Settings.Core.Interfaces;
using DotNetBoostSample.Api.Endpoints;
using DotNetBoostSample.Api.Integration;
using FluentValidation;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetBoostSample.Api.Settings;

// One settings group per file, with everything that group needs: the class, the rules that bound
// its values, and — where it has one — the projector that shapes its read and the response type
// that projector publishes. The validator and the projector are not independent components; each
// names exactly one group in its own signature, so they are only ever read beside it. They used to
// live in files collected by KIND — SettingsValidators.cs, BrandingProjector.cs — which meant
// adding a property to a group and the rule bounding it was two files, and seeing what a group
// amounts to was three.
//
// What stays out: MultiLingualSchemaContributor, which belongs to no group — it describes any
// settings type carrying [MultiLingual], and keeps its own file for that reason.

/// <summary>
/// The site's branding: runtime-editable text, and — through the media library rather than through
/// this class — a logo.
///
/// <para><b>Why the logo is not a property here.</b> It was, as a <c>Guid LogoFileId</c>, and that is
/// a perfectly ordinary way to reference a file. But a file referenced only by a setting's value is
/// invisible to DotNetBoost.Media: it appears in no entity's attachments, its details panel says it
/// is used by nothing, and deleting it warns about nothing. Attaching it as well fixed the reading
/// but left two records of one fact, which drifted the moment anyone detached the file from the
/// media page — the attachment went, the setting did not.</para>
///
/// <para>So the attachment is now the only record. There is nothing to keep in step, and detaching
/// the logo means the site has no logo, because that is the same statement.</para>
///
/// <para><b>What that costs.</b> The logo is no longer part of this settings group, so it is absent
/// from <c>GET /api/settings/branding</c>, from the group's ETag, and from its audit trail. Those
/// are real losses, and they are what storing an id in a setting buys. They are given up here in
/// exchange for a single source of truth.</para>
///
/// <para><b>The owner.</b> Branding has no row anywhere — it is one group of setting rows, and there
/// can only ever be one of it. The media library does not care: an owner is two strings, and it
/// never tries to resolve them to anything. That is what lets a thing with no table own files, and
/// it is the sharpest demonstration of the attachment model in this sample — a foreign key could
/// not do this.</para>
/// </summary>
[SettingGroup("branding")]
// Pins the name the attachments are stored under, so renaming this class cannot orphan them.
// Without it the stored name would follow the class — "BrandingSettings" today, whatever it is
// called tomorrow, and every existing row addressing the old one.
[MediaEntity(MediaOwnerName)]
// The same name on the MultiLingual side, so both libraries address branding as the same subject:
// ("Branding", "branding") would then carry the logo attachment AND StoreName's translations.
// Two independent attributes from two libraries that share no registry — they agree because the
// same word is written twice. Nothing in either library would report it if one of the two changed,
// so BrandingOwnerTests asserts the two resolve to the same name.
[MultiLingualEntity(MediaOwnerName)]
// Declared on the class rather than in Program.cs, so the roles live next to the thing that has
// them: open this file and you can see what branding holds. Read by DeclareRoles<BrandingSettings>().
// A second, multi-valued role is one more line — [MediaRole("banners", Multiple = true)].
[MediaRole(LogoRole)]
public class BrandingSettings
{
    /// <summary>The owner name these attachments are filed under.</summary>
    public const string MediaOwnerName = "Branding";

    /// <summary>
    /// The owner id. Degenerate, because there is only ever one branding — the same way a config
    /// table with a single row conventionally uses <c>Id = 1</c>. It is the group's own route from
    /// <see cref="SettingGroupAttribute"/> rather than a new invention, and it has to be
    /// <em>something</em>: the media library requires both halves of an owner address, which is
    /// what lets one table serve every owner with no column per owner type.
    /// </summary>
    public const string MediaOwnerId = "branding";

    /// <summary>The role the logo is attached under. Exactly one per owner.</summary>
    public const string LogoRole = "logo";

    /// <summary>
    /// The id MultiLingual addresses this group by — the other half of
    /// <see cref="MultiLingualEntityAttribute"/>'s name. A translation row is
    /// <c>(EntityName, EntityId, PropertyName, LanguageCode)</c>, and without an id there is no
    /// row to write: the name alone is half an address.
    ///
    /// <para><b>Get-only on purpose, and that is the whole trick.</b> The settings library maps
    /// only properties that are <c>CanRead &amp;&amp; CanWrite</c>, so this one is invisible to it
    /// and never becomes a setting row. MultiLingual's convention accessor looks for exactly this
    /// name and only ever reads it — and its EF accessor falls back to that convention for types
    /// outside the model, which this is. One property, two libraries, each seeing what it needs
    /// and neither knowing the other exists.</para>
    ///
    /// <para>The same degenerate constant the media owner uses, so one subject —
    /// <c>("Branding", "branding")</c> — carries the logo attachment and this group's translations
    /// alike.</para>
    ///
    /// <para><b>And ignored on the wire.</b> Being get-only keeps it out of the STORE, because
    /// SettingManager maps only read-write properties — but it does not keep it out of the
    /// RESPONSE: System.Text.Json serialises read-only properties, so <c>GET
    /// /api/settings/branding</c> was returning it and the dashboard's generated form grew a
    /// pointless "Id" box for it. It is an addressing detail, not one of the group's values.</para>
    /// </summary>
    [JsonIgnore]
    public string Id => MediaOwnerId;

    /// <summary>
    /// Shown in the dashboard header.
    /// <para>
    /// The attribute goes BELOW the doc comment: a <c>///</c> block between an attribute and its
    /// member is CS1587, and is dropped from IntelliSense even where documentation generation is
    /// off and the warning never fires.
    /// </para>
    /// </summary>
    [MultiLingual]
    public string StoreName { get; set; } = "DotNetBoost Sample Store";

    /// <summary>Accent colour, as a CSS hex value.</summary>
    public string AccentColor { get; set; } = "#512BD4";

    /// <summary>
    /// How large the logo is drawn in the dashboard header, in pixels.
    /// <para>
    /// A setting rather than a constant in the client because it is exactly the kind of thing
    /// someone wants to nudge after seeing their own logo in place — which is what this library is
    /// for: change it, reload, done, with no redeploy. The logo it sizes is not stored here, and
    /// the two are independent: this is a number whether or not a logo is attached.
    /// </para>
    /// <para>
    /// Bounded 16–64 by <see cref="BrandingSettingsValidator"/> just below, so a careless value is
    /// refused rather than clamped at render time — and <c>GET /api/settings/branding/schema</c>
    /// publishes that range, which is where the dashboard's number control gets its own. The range
    /// used to live only in the client and match nothing here.
    /// </para>
    /// </summary>
    public int HeaderLogoSize { get; set; } = 28;
}

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
/// What <c>GET /api/settings/branding</c> answers with — the type behind
/// <see cref="BrandingProjector"/>, and the one the OpenAPI document describes.
///
/// <para><b>Why this type exists.</b> The projector used to build a dictionary, which was correct
/// JSON that nothing could describe: the generated endpoint declared the STORED group as its 200, so
/// the document, Scalar and any generated client claimed three properties for a response carrying
/// six. A reader following it found no <c>resolved</c>, no <c>images</c> and no <c>logo</c> on a
/// response that has all three. Naming the shape is what makes it publishable — and because
/// <see cref="BrandingProjector"/> declares it through
/// <c>ISettingProjector&lt;BrandingSettings, BrandingResponse&gt;</c>, the compiler keeps the
/// published type and the returned type the same one.</para>
///
/// <para><b>Why it derives from the group.</b> So the stored values are written once. Every
/// readable, writable property of <see cref="BrandingSettings"/> is part of this response by
/// inheritance, and the projector copies them by the same rule the settings engine maps them by —
/// which means adding a property to the group puts it in the response AND in the published schema
/// with no edit in either file. Naming them here would be the mistake that left the dashboard's
/// per-language editor stuck on one box, made twice.</para>
///
/// <para><b>Deriving from a settings group is safe, and deliberately so in both libraries.</b>
/// <c>[SettingGroup]</c> is declared <c>Inherited = false</c>, so this class is not discovered as a
/// second group claiming the route <c>branding</c>; DotNetBoost.Media's role scan reads
/// <c>[MediaRole]</c> with <c>inherit: false</c> for the same reason, so it does not file branding's
/// <c>logo</c> role a second time under this name. The <c>[JsonIgnore]</c> on
/// <c>BrandingSettings.Id</c> is inherited, so the addressing detail stays off the wire here
/// too.</para>
///
/// <para><b>Only the read.</b> <c>POST /api/settings/branding</c> still takes
/// <see cref="BrandingSettings"/>, and the document still says so. That asymmetry is what a
/// read-path projector IS: the response carries another library's data beside the settings, and
/// none of it is writable here. Translations go to
/// <c>PUT /api/translations/Branding/branding</c> and the logo to
/// <c>POST /api/media/{id}/attachments</c>.</para>
/// </summary>
public sealed class BrandingResponse : BrandingSettings
{
    /// <summary>
    /// The group's <c>[MultiLingual]</c> properties in the language the request was read in, keyed
    /// by the property's own JSON name, for the ones a translation exists for. Empty for a language
    /// nobody has written.
    ///
    /// <para>Beside the stored values rather than over them: the settings form loads this response
    /// in the reader's language and posts it back, and the row it writes holds the DEFAULT language.
    /// A caller rendering branding reads <c>resolved.storeName ?? storeName</c>; a caller editing it
    /// reads <c>storeName</c> and is right by default. <see cref="BrandingProjector"/> has the long
    /// version.</para>
    /// </summary>
    public IReadOnlyDictionary<string, string> Resolved { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Everything attached to the owner <c>("Branding", "branding")</c>, with roles — the same shape
    /// a product's images come back in, which is what lets the dashboard draw a settings group's
    /// media with the component it draws a product's with.
    /// </summary>
    public IReadOnlyList<AttachedFileInRole> Images { get; init; } = [];

    /// <summary>
    /// The file in the <c>logo</c> role, or <c>null</c>. The property name is the role, so the file
    /// does not repeat it.
    /// </summary>
    public AttachedFile? Logo { get; init; }
}

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
/// <para><b>The shape is declared, not just returned.</b> This implements
/// <c>ISettingProjector&lt;BrandingSettings, BrandingResponse&gt;</c>, so <c>GET</c>'s published 200
/// is <see cref="BrandingResponse"/> rather than the stored group. The untyped interface returns
/// <c>object</c> and leaves the generated endpoint declaring the group, which described three
/// properties for a body carrying six — a wrong schema, which is worse than none because it is
/// specific and believed. Here the type the method returns IS the type published, so they cannot
/// drift.</para>
///
/// <para><b>No property is named here.</b> The values are read off the type and the translatable
/// ones come from <c>MultiLingualMetadata</c>, so adding a property to
/// <see cref="BrandingSettings"/> — translatable or not — needs no edit below. Naming them is what
/// left the dashboard's per-language editor stuck on one box.</para>
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

        // The translatable properties resolved for this request, under a SEPARATE key — never over
        // the stored value.
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
        //
        // The keys are camel-cased here because these are dictionary KEYS: JsonSerializerDefaults
        // .Web camel-cases property names and leaves dictionary keys alone, so `resolved` would
        // otherwise answer `StoreName` beside a `storeName` it is meant to pair with.
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var translatable in MultiLingualMetadata.For(typeof(BrandingSettings)))
        {
            if (InLanguage(stored, translatable.Name) is { } translated)
                resolved[JsonNamingPolicy.CamelCase.ConvertName(translatable.Name)] = translated;
        }

        var owner = media.For(BrandingSettings.MediaOwnerName, BrandingSettings.MediaOwnerId);
        var logo  = await owner.GetByRoleAsync(BrandingSettings.LogoRole, ct);

        // The media, which is not a setting and is deliberately not among the group's own
        // properties — GroupForm draws its boxes from the schema's property list, so these two
        // travel in the same response without becoming inputs on the form.
        var response = new BrandingResponse
        {
            Resolved = resolved,
            Images   = await AttachedFiles.DescribeAsync(owner, mediaText, ct),
            Logo     = logo is null
                ? null
                : AttachedFiles.Describe(logo, await mediaText.GetAsync(logo.Id, ct))
        };

        // The group's own values, copied by the rule the settings engine maps them by — asked for
        // rather than restated. SettingSchema.StorableProperties IS that rule, and SettingManager
        // builds its own map from the same call, so this cannot copy a property the engine ignores
        // or miss one it stores.
        //
        // Copied rather than listed, so adding a property to BrandingSettings needs no edit here —
        // which is exactly what naming them cost the dashboard's per-language editor.
        // BrandingResponse derives from the group, so the property being copied is the same
        // property, and the response has nowhere to put a typo.
        foreach (var property in SettingSchema.StorableProperties(typeof(BrandingSettings)))
            property.SetValue(response, property.GetValue(group));

        return response;
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
