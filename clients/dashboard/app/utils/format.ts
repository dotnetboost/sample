/**
 * Presentation helpers for a stored file. Nothing here talks to the API — it turns what the API
 * already said into something readable.
 *
 * "Readable" is locale-dependent, so every one of them goes through `intlLocale()` rather than
 * through `undefined`, which would have meant the browser's language. The browser's language is
 * not the dashboard's: someone whose Chrome is in German reading this dashboard in French should
 * see "28 sept. 2026", not "28. Sept. 2026".
 */

/**
 * A size in bytes as a short human string: `1.4MB`, `1,4Mo`, `1.4 م.ب`.
 *
 * The unit name comes from `Intl` rather than from the message bundles, because it is the one
 * piece of this dashboard's text that is already translated into every locale the runtime knows —
 * French says "o" for octet and Arabic says "ب", and neither is something this file should be
 * asserting. It also means a locale added later is formatted correctly before anyone writes it a
 * bundle.
 */
export function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes < 0) {
    return '—'
  }

  // Intl's own unit identifiers, which is why they are spelled out rather than abbreviated.
  const units = ['byte', 'kilobyte', 'megabyte', 'gigabyte', 'terabyte'] as const

  let value = bytes
  let unit = 0

  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit++
  }

  return new Intl.NumberFormat(intlLocale(), {
    style: 'unit',
    unit: units[unit],
    // "short" is inconsistent across locales — Arabic spells "كيلوبايت" out but abbreviates
    // "م.ب", and English gives "912 byte" for a single byte. "narrow" is the same shape in all
    // three.
    unitDisplay: 'narrow',
    // One decimal below 10, none above: "9.4MB" is useful, "943.7MB" is noise. Whole bytes never
    // take a decimal at all.
    maximumFractionDigits: unit === 0 ? 0 : value < 10 ? 1 : 0
  }).format(value)
}

/** A UTC timestamp from the API as a local date and time, written for the UI locale. */
export function formatDate(iso: string | null | undefined): string {
  if (!iso) {
    return '—'
  }

  const date = new Date(iso)
  return Number.isNaN(date.getTime())
    ? '—'
    : date.toLocaleString(intlLocale(), { dateStyle: 'medium', timeStyle: 'short' })
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
      return new Intl.RelativeTimeFormat(intlLocale(), { numeric: 'auto' }).format(-Math.round(value), unit)
    }
    value /= size
  }

  return new Intl.RelativeTimeFormat(intlLocale(), { numeric: 'auto' }).format(-Math.round(value), 'year')
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
