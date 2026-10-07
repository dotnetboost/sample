using DotNetBoostSample.Api.Endpoints;

namespace DotNetBoostSample.Api.Settings;

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
