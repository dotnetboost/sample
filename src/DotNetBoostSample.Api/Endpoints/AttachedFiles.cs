using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.Media.Core.Models;
using DotNetBoostSample.Api.Integration;

namespace DotNetBoostSample.Api.Endpoints;

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
    public static async Task<IReadOnlyList<object>> DescribeAsync(
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

    /// <summary>
    /// One file on its own — the shape a listing and a thumbnail carry.
    ///
    /// <para><b>No role, and that is the point.</b> A role belongs to an ATTACHMENT, not to a file:
    /// the media library lets one file be attached several times over, so a product's primary image
    /// can also sit in its gallery. A listing answers "what files does this product have", which has
    /// no single role to report. The products list used to send <c>role: ""</c> and
    /// <c>sortOrder: 0</c> here — the same field names the detail endpoint fills with real values,
    /// carrying the one value a role can never truthfully take. Absent beats blank: a reader that
    /// needs roles now gets a 404 on the property instead of a plausible empty string.</para>
    /// </summary>
    public static object Describe(MediaFileInfo file, MediaTextValues? text) => new
    {
        file.Id,
        file.FileName,
        file.ContentType,
        file.Size,
        AltText = text?.AltText,
        Caption = text?.Caption
    };

    /// <summary>
    /// One file in one of an entity's roles — what the dashboard's role boxes read.
    ///
    /// <para>The same shape as <see cref="Describe(MediaFileInfo, MediaTextValues?)"/> plus the two
    /// fields that only exist once a file is attached to something. The duplication is deliberate
    /// and small: an anonymous object cannot be extended, and nesting the file under a property
    /// would change the JSON every caller reads. Add a field to one and add it to the other.</para>
    /// </summary>
    public static object DescribeInRole(
        MediaFileInfo file, string role, int sortOrder, MediaTextValues? text) => new
    {
        file.Id,
        file.FileName,
        file.ContentType,
        file.Size,
        Role      = role,
        SortOrder = sortOrder,
        AltText   = text?.AltText,
        Caption   = text?.Caption
    };
}
