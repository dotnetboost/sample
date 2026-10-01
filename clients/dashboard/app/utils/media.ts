/**
 * Where a media file's bytes are served from.
 *
 * The media library's own endpoints return `url` as a SIGNED, expiring link — which is null on
 * the FileSystem backend, because local disk has nothing to sign. So the URL is derived from the
 * id rather than read from the payload: that works on every backend, and switching from local
 * disk to Azure or S3 does not change a line of this dashboard.
 */
export const mediaUrl = (file: { id: string } | null | undefined) =>
  file ? `/api/media/${file.id}/content` : undefined
