using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.Media.Core.Models;
using MediaEntityKey = DotNetBoost.Media.Core.Metadata.EntityKey;
using DotNetBoostSample.Application.Composition;

namespace DotNetBoostSample.Application.Catalog.Shared;

/// <summary>
/// Describes one owner's attached files for the dashboard: each file with the role it is attached
/// under, its position in that role, and its alt text.
///
/// <para>Shared by the catalogue and by branding, because the shape is the same for every owner —
/// which is the point. A category, a product and a settings group are all just two strings to the
/// media library, so one description serves all three and the dashboard draws them with one
/// component.</para>
///
/// <para><b>No URL, deliberately.</b> These shapes used to carry
/// <c>"/api/media/{id}/content"</c>, built here from the id that is already in them, while
/// <c>contentUrl</c> in the dashboard's app/utils/format.ts built the identical string for every
/// image it renders — and nothing ever read the one sent from here. Two copies of one rule, one of
/// them dead.</para>
///
/// <para>The client's copy is the one that has to exist. DotNetBoost.Media's own <c>url</c> is a
/// signed, expiring link that is <c>null</c> on the FileSystem backend and would 403 in a
/// long-open tab on the others, so the dashboard cannot simply read a field — it derives the route
/// from the id, which is also what keeps a move to Azure or S3 from touching any client code. A
/// caller of these endpoints does the same: the id is here, and the route is one line of the
/// README away.</para>
/// </summary>
internal static class AttachedFiles
{
    /// <summary>
    /// Every file attached to <paramref name="accessor"/>'s owner.
    /// <para>
    /// <c>ListWithRolesAsync</c> rather than a call per role: a file attached under two roles comes
    /// back once per role, which is exactly right for role boxes, and the alt text for the whole
    /// set is resolved in one query.
    /// </para>
    /// </summary>
    public static async Task<IReadOnlyList<AttachedFileInRole>> DescribeAsync(
        IMediaAccessor accessor,
        MediaText mediaText,
        CancellationToken ct)
    {
        var attached = await accessor.ListWithRolesAsync(ct);

        if (attached.Count == 0) return [];

        var text = await mediaText.GetManyAsync([.. attached.Select(a => a.File.Id)], ct);

        return
        [
            .. attached
                .OrderBy(a => a.Role)
                .ThenBy(a => a.SortOrder)
                .Select(a => DescribeInRole(a.File, a.Role, a.SortOrder, text.GetValueOrDefault(a.File.Id)))
        ];
    }

    /// <summary>One file on its own — the shape a listing and a thumbnail carry.</summary>
    public static AttachedFile Describe(MediaFileInfo file, MediaTextValues? text) =>
        new(file.Id, file.FileName, file.ContentType, file.Size, text?.AltText, text?.Caption);

    /// <summary>
    /// An owner's key as DotNetBoost.Media formats it, which is how a bulk read keys its answer.
    /// Through the library's own formatter rather than <c>id.ToString()</c>, so this application
    /// is not relying on the two happening to agree.
    /// </summary>
    public static string OwnerKey(int id) => MediaEntityKey.ToKeyString(id);

    /// <summary>One file in one of an entity's roles — what the dashboard's role boxes read.</summary>
    public static AttachedFileInRole DescribeInRole(
        MediaFileInfo file, string role, int sortOrder, MediaTextValues? text) =>
        new(file.Id, file.FileName, file.ContentType, file.Size, role, sortOrder,
            text?.AltText, text?.Caption);
}

/// <summary>
/// One stored file as this API describes it: the media row's public facts plus the alt text and
/// caption, which are translation rows rather than file columns.
///
/// <para><b>A named record rather than an anonymous object.</b> These were anonymous, which is
/// invisible to OpenAPI: a response built out of <c>new { … }</c> has no type to generate a schema
/// from, so the document described the endpoint's body as an untyped object — or, where some other
/// type was declared for it, described the wrong one. The JSON is unchanged, field for field and
/// in the same order; what changed is that the document can now say what it is. Branding needed it
/// first: <see cref="DotNetBoostSample.Application.Settings.BrandingResponse"/> carries these two, and a
/// schema naming them is only worth as much as the shapes inside it.</para>
///
/// <para><b>The size is a number of bytes</b>, and the id is the media file's own — the route to
/// its content is derived from it by the caller, as the class above explains.</para>
/// </summary>
public sealed record AttachedFile(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    string? AltText,
    string? Caption);

/// <summary>
/// One file in one of an entity's roles — <see cref="AttachedFile"/> plus the two facts that only
/// exist once a file is attached to something.
///
/// <para><b>Role is on the attachment, not on the file, and that is why this is a second type
/// rather than two nullable fields on the first.</b> The media library lets one file be attached
/// several times over, so a product's primary image can also sit in its gallery: a listing answers
/// "what files does this product have", which has no single role to report. The products list used
/// to send <c>role: ""</c> and <c>sortOrder: 0</c> — the same field names the detail endpoint fills
/// with real values, carrying the one value a role can never truthfully take. Absent beats blank: a
/// reader that needs roles now gets a 404 on the property instead of a plausible empty string, and
/// the schema says which responses have them.</para>
///
/// <para>The repeated fields are deliberate and small. Deriving from <see cref="AttachedFile"/>
/// would group a file's own facts under a base type in the document and buy nothing on the wire;
/// nesting the file under a property would change the JSON every caller reads. Add a field to one
/// and add it to the other.</para>
/// </summary>
public sealed record AttachedFileInRole(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    string Role,
    int SortOrder,
    string? AltText,
    string? Caption);
