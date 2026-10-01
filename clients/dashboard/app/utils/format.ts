/**
 * Presentation helpers for a stored file. Nothing here talks to the API — it turns what the API
 * already said into something readable.
 */

/** A size in bytes as a short human string: `1.4 MB`. */
export function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes < 0) {
    return '—'
  }

  if (bytes < 1024) {
    return `${bytes} B`
  }

  const units = ['KB', 'MB', 'GB', 'TB']
  let value = bytes / 1024
  let unit = 0

  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit++
  }

  // One decimal below 10, none above: "9.4 MB" is useful, "943.7 MB" is noise.
  return `${value < 10 ? value.toFixed(1) : Math.round(value)} ${units[unit]}`
}

/** A UTC timestamp from the API as a local date and time. */
export function formatDate(iso: string | null | undefined): string {
  if (!iso) {
    return '—'
  }

  const date = new Date(iso)
  return Number.isNaN(date.getTime())
    ? '—'
    : date.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

/** A UTC timestamp as "3 hours ago", falling back to the date once it is old. */
export function formatRelative(iso: string | null | undefined): string {
  if (!iso) {
    return '—'
  }

  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) {
    return '—'
  }

  const seconds = (Date.now() - date.getTime()) / 1000
  const units: [Intl.RelativeTimeFormatUnit, number][] = [
    ['second', 60], ['minute', 60], ['hour', 24], ['day', 7], ['week', 4.348], ['month', 12]
  ]

  let value = seconds
  for (const [unit, size] of units) {
    if (Math.abs(value) < size) {
      return new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' }).format(-Math.round(value), unit)
    }
    value /= size
  }

  return new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' }).format(-Math.round(value), 'year')
}

/** Whether a media type is one a browser will render as an image. */
export function isImage(contentType: string): boolean {
  // SVG is an image the browser renders by executing it. The dashboard shows it as an icon rather
  // than inline for the same reason the docs say to serve user uploads from another origin.
  return contentType.startsWith('image/') && contentType !== 'image/svg+xml'
}

/** A Lucide icon for a media type, for the files that cannot be shown as a thumbnail. */
export function iconFor(contentType: string): string {
  if (contentType.startsWith('image/')) return 'i-lucide-image'
  if (contentType.startsWith('video/')) return 'i-lucide-film'
  if (contentType.startsWith('audio/')) return 'i-lucide-music'
  if (contentType.startsWith('text/')) return 'i-lucide-file-text'
  if (contentType === 'application/pdf') return 'i-lucide-file-type-2'

  if (contentType.includes('zip') || contentType.includes('compressed') || contentType.includes('tar')) {
    return 'i-lucide-file-archive'
  }

  if (contentType.includes('spreadsheet') || contentType.includes('excel') || contentType === 'text/csv') {
    return 'i-lucide-table'
  }

  if (contentType.includes('word') || contentType.includes('document')) return 'i-lucide-file-text'
  if (contentType.includes('presentation') || contentType.includes('powerpoint')) return 'i-lucide-presentation'
  if (contentType.startsWith('font/')) return 'i-lucide-type'

  return 'i-lucide-file'
}

/** The dashboard's own route to a file's bytes, which is proxied through to the API. */
export function contentUrl(id: string, download = false): string {
  return `/api/media/${id}/content${download ? '?download=true' : ''}`
}

/** The short form of a media type, for a badge: `image/png` → `PNG`. */
export function contentTypeLabel(contentType: string): string {
  const subtype = contentType.split('/')[1] ?? contentType
  // "vnd.openxmlformats-officedocument.wordprocessingml.document" → "DOCUMENT"
  const last = subtype.split(/[.+]/).pop() ?? subtype
  return last.toUpperCase()
}
