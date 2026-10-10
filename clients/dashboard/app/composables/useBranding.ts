import type { EntityImage, MediaOwner, MediaRef } from '~/types'

export interface Branding {
  /**
   * As STORED — the default language's value, which the settings row holds and the settings form
   * edits. Use `resolved.storeName ?? storeName` to display it; see `resolved`.
   */
  storeName: string
  accentColor: string
  /** How large to draw the logo in the header, in pixels. */
  headerLogoSize: number
  /** Everything attached to the owner below, with roles. */
  images: EntityImage[]
  /**
   * The two strings `images` are filed under, so a caller that wants to attach knows where. The
   * settings page reads it from here instead of writing `'Branding'` and `'branding'` down.
   */
  owner: MediaOwner
  /** The file in the `logo` role, or null. The field name is the role, so the file does not repeat it. */
  logo: MediaRef | null
  /**
   * The group's `[MultiLingual]` properties in the language the request was read in, for the ones
   * a translation exists for. Empty for a language nobody has written.
   *
   * Beside the stored values rather than over them, deliberately: the settings form loads this
   * same response with the reader's language and posts it back, and the row it writes holds the
   * DEFAULT language. If the projection replaced `storeName`, editing branding while reading in
   * Arabic would save the Arabic text into the English row. So a renderer reads from here and
   * falls back; an editor reads the property and is right without knowing any of this.
   */
  resolved: Partial<Record<'storeName', string>>
}

/** The key the header and the settings page share, so one refresh updates both. */
export const BRANDING_KEY = 'branding'

/**
 * The store's branding, fetched once and shared.
 *
 * The header and the branding settings page both read it under the same `useAsyncData` key, so
 * `refreshNuxtData(BRANDING_KEY)` after a save or an attach updates the header without either
 * knowing about the other.
 *
 * It composes two libraries that know nothing of each other: the name and the logo size are
 * DotNetBoost.Settings values; the logo itself is a DotNetBoost.Media attachment on an owner with
 * no table. The API joins them in `BrandingProjector` (in `Settings/BrandingSettings.cs`), on
 * the settings group's own endpoint, and publishes the joined shape as `BrandingResponse`.
 */
export function useBranding() {
  const api = useApi()
  const language = useLanguage()

  // Re-fetch when the reading language changes. The call sends Accept-Language, but useAsyncData
  // caches by key alone — without this the header keeps whatever language it was first loaded in,
  // which is exactly wrong for the one value on screen that is supposed to follow the language.
  return useAsyncData<Branding>(BRANDING_KEY, () => api.branding.get(), { watch: [language] })
}
