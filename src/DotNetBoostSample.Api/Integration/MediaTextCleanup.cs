using DotNetBoost.Media.Core.Interfaces;

namespace DotNetBoostSample.Api.Integration;

/// <summary>
/// Removes a file's per-language alt text when the file itself is deleted — registered once, with
/// <c>.OnFilesDeleted&lt;MediaTextCleanup&gt;()</c>, instead of remembered at every call site.
///
/// <para><b>What this replaces.</b> Every endpoint that deleted a file used to delete its
/// translation rows too, in a loop beside the delete. The sample got that wrong in two ways at
/// once: the product delete called <c>IMediaManager.DeleteAsync</c> per file, which removes a file
/// from <em>every</em> owner, so deleting one product pulled shared images out of the others; and
/// deleting a file through DotNetBoost.Media's own <c>DELETE /api/media/{id}</c> cleaned up
/// nothing at all, because only the dashboard knew to follow up.</para>
///
/// <para><b>Why a handler fixes both.</b> It is told which files actually went — Media's
/// <c>DeleteAllAsync</c> reports the ones it deleted and <b>not</b> the ones it merely detached,
/// because those are still in use and their alt text must survive with them. So the cleanup is
/// exact, it happens for every delete whatever triggered it, and no call site has to remember it.
/// A handler's exceptions are logged and swallowed by the library: the bytes are already gone, so
/// failing here would report a completed delete as a failure.</para>
///
/// <para>Note what this still is not: an integration package. DotNetBoost.Media raises an event
/// about its own files, DotNetBoost.MultiLingual stores rows about a subject, and the only thing
/// that knows those are the same thing is this class.</para>
/// </summary>
public sealed class MediaTextCleanup(MediaText mediaText) : IMediaFilesDeletedHandler
{
    /// <inheritdoc/>
    public Task OnDeletedAsync(IReadOnlyCollection<Guid> mediaFileIds, CancellationToken ct = default) =>
        mediaText.DeleteManyAsync(mediaFileIds, ct);
}
