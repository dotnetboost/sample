import type { EntityImage, MediaRef } from '~/types'

export interface Branding {
  storeName: string
  accentColor: string
  /** How large to draw the logo in the header, in pixels. */
  headerLogoSize: number
  /** Everything attached to ("Branding", "branding"), with roles. */
  images: EntityImage[]
  /** The file in the `logo` role, or null. The field name is the role, so the file does not repeat it. */
  logo: MediaRef | null
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
 * no table. The API joins them in `BrandingEndpoints.cs`.
 */
export function useBranding() {
  const api = useApi()
  const language = useLanguage()

  // Re-fetch when the reading language changes. The call sends Accept-Language, but useAsyncData
  // caches by key alone — without this the header keeps whatever language it was first loaded in,
  // which is exactly wrong for the one value on screen that is supposed to follow the language.
  return useAsyncData<Branding>(BRANDING_KEY, () => api.branding.get(), { watch: [language] })
}
