using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.Media.Core.Models;
using DotNetBoostSample.Api.Integration;

namespace DotNetBoostSample.Api.Endpoints;

/// <summary>
/// Describes one owner's attached files for the dashboard: each file with the role it is attached
/// under, its position in that role, and its alt text.
/// <para>
/// Shared by the catalogue and by branding, because the shape is the same for every owner — which
/// is the point. A category, a product and a settings group are all just two strings to the media
/// library, so one description serves all three and the dashboard draws them with one component.
/// </para>
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
                .Select(a => Describe(a.File, a.Role, a.SortOrder, text.GetValueOrDefault(a.File.Id)))
        ];
    }

    /// <summary>One file, in the shape the dashboard's role boxes read.</summary>
    public static object Describe(MediaFileInfo file, string role, int sortOrder, MediaTextValues? text) => new
    {
        file.Id,
        file.FileName,
        file.ContentType,
        file.Size,
        Role      = role,
        SortOrder = sortOrder,
        AltText   = text?.AltText,
        Caption   = text?.Caption,
        Url       = $"/api/media/{file.Id}/content"
    };
}
