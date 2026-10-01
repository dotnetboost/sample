import type { EntityImage } from '~/types'

export interface Branding {
  storeName: string
  accentColor: string
  /** How large to draw the logo in the header, in pixels. */
  headerLogoSize: number
  /** Everything attached to ("Branding", "branding"), with roles. */
  images: EntityImage[]
  /** The file in the `logo` role, or null. */
  logo: EntityImage | null
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

  return useAsyncData<Branding>(BRANDING_KEY, () => api.branding.get())
}
