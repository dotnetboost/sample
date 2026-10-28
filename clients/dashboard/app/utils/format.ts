import type { Component } from 'vue'
import { File, FileArchive, FileText, FileType2, Film, Image, Music, Presentation, Table, Type } from '@lucide/vue'

/**
 * Presentation helpers for a stored file. Nothing here talks to the API — it turns what the API
 * already said into something readable.
 *
 * "Readable" is locale-dependent, so every one of them goes through `intlLocale()` rather than
 * through `undefined`, which would have meant the browser's language. The browser's language is
 * not the dashboard's: someone whose Chrome is in German reading this dashboard in French should
 * see "28 sept. 2026", not "28. Sept. 2026".
 */

/** What is shown in place of a value that is missing or unreadable. */
const NO_VALUE = '—'

/**
 * A timestamp from the API as a `Date`, or null for anything that is not one.
 *
 * Both cases reach here: the API sends null for a date it has no value for, and `new Date` answers
 * an Invalid Date — which formats as "Invalid Date", in English, on the page — for a string it
 * cannot read. Neither is worth telling the user about, and both show as {@link NO_VALUE}.
 */
function parseTimestamp(iso: string | null | undefined): Date | null {
  if (!iso) {
    return null
  }

  const date = new Date(iso)
  return Number.isNaN(date.getTime()) ? null : date
}

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
    return NO_VALUE
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
  const date = parseTimestamp(iso)

  return date
    ? date.toLocaleString(intlLocale(), { dateStyle: 'medium', timeStyle: 'short' })
    : NO_VALUE
}

/**
 * A calendar day (`yyyy-mm-dd`) written for the UI locale. Read and written in UTC: it is a day,
 * not an instant, and in local time "the 24th" would print as the 23rd west of Greenwich.
 */
export function formatDay(day: string): string {
  const date = new Date(`${day}T00:00:00Z`)

  return Number.isNaN(date.getTime())
    ? NO_VALUE
    : date.toLocaleDateString(intlLocale(), { dateStyle: 'medium', timeZone: 'UTC' })
}

/** A UTC timestamp as "3 hours ago", falling back to the date once it is old. */
export function formatRelative(iso: string | null | undefined): string {
  const date = parseTimestamp(iso)

  if (!date) {
    return NO_VALUE
  }

  const relative = new Intl.RelativeTimeFormat(intlLocale(), { numeric: 'auto' })

  // Each size is how many of that unit make up the next one, so dividing through the list carries
  // the value up until one of them can hold it. Years are the end of the list, not a unit in it.
  const units: [Intl.RelativeTimeFormatUnit, number][] = [
    ['second', 60], ['minute', 60], ['hour', 24], ['day', 7], ['week', 4.348], ['month', 12]
  ]

  let value = (Date.now() - date.getTime()) / 1000

  for (const [unit, size] of units) {
    if (Math.abs(value) < size) {
      return relative.format(-Math.round(value), unit)
    }
    value /= size
  }

  return relative.format(-Math.round(value), 'year')
}

/** Whether a media type is one a browser will render as an image. */
export function isImage(contentType: string): boolean {
  // SVG is an image the browser renders by executing it. The dashboard shows it as an icon rather
  // than inline for the same reason the docs say to serve user uploads from another origin.
  return contentType.startsWith('image/') && contentType !== 'image/svg+xml'
}

/** A Lucide icon for a media type, for the files that cannot be shown as a thumbnail. */
export function iconFor(contentType: string): Component {
  if (contentType.startsWith('image/')) return Image
  if (contentType.startsWith('video/')) return Film
  if (contentType.startsWith('audio/')) return Music
  if (contentType.startsWith('text/')) return FileText
  if (contentType === 'application/pdf') return FileType2

  if (contentType.includes('zip') || contentType.includes('compressed') || contentType.includes('tar')) {
    return FileArchive
  }

  if (contentType.includes('spreadsheet') || contentType.includes('excel') || contentType === 'text/csv') {
    return Table
  }

  if (contentType.includes('word') || contentType.includes('document')) return FileText
  if (contentType.includes('presentation') || contentType.includes('powerpoint')) return Presentation
  if (contentType.startsWith('font/')) return Type

  return File
}

/**
 * A price in the UI locale, with the currency symbol from DotNetBoost.Settings.
 *
 * `Intl` needs a currency CODE to place a symbol, because placement is the locale's business and
 * not the symbol's: English writes `$349.00`, French `349,00 $`, and each uses its own separators.
 * The setting is a glyph and nothing else, so `USD` stands in purely to get that shape, and
 * `formatToParts` swaps the symbol it resolved for the configured one. The digits, the separators
 * and the position are the locale's; only the glyph is the setting's.
 *
 * Two decimals follow from the same stand-in, and that is the honest limit of a symbol: `$` says
 * nothing about its currency's minor unit, so nothing here could know that yen takes none. A
 * settings group holding a currency *code* could, and would not need this function.
 */
export function formatPrice(value: number, symbol: string): string {
  if (!Number.isFinite(value)) {
    return NO_VALUE
  }

  return new Intl.NumberFormat(intlLocale(), { style: 'currency', currency: 'USD' })
    .formatToParts(value)
    .map(part => (part.type === 'currency' ? symbol : part.value))
    .join('')
}

/** The dashboard's own route to a file's bytes, which is proxied through to the API. */
export function contentUrl(id: string, download = false): string {
  return `/api/media/${id}/content${download ? '?download=true' : ''}`
}
