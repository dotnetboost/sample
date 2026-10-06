/** The `catalog` settings group, as `GET api/settings/catalog` returns it. */
export interface CatalogSettings {
  /** Products per page, when a caller asks for no page size of its own. */
  pageSize: number
  /** Drawn beside a price. A glyph only — see `formatPrice`, which has to place it. */
  currencySymbol: string
}

/** The key the catalogue page and the settings tab share, so one refresh updates both. */
export const CATALOG_SETTINGS_KEY = 'catalog-settings'

/**
 * How the catalogue is presented, fetched once and shared — the read-only twin of
 * `useSettingsGroup('catalog')`, which is the form that writes it.
 *
 * Only the values a screen cannot infer from what it was already sent are read here.
 * `pageSize` is not one of them: the API applies it and answers with `take`, so a pager reads the
 * page it was actually given rather than re-deciding it. What the client genuinely needs is
 * `currencySymbol`, which no listing carries and which `formatPrice` needs in order to place.
 *
 * Shares `useBranding`'s pattern deliberately, including the key: `refreshNuxtData` after a save
 * on the Settings page updates the catalogue without either knowing about the other.
 */
export function useCatalogSettings() {
  const api = useApi()

  return useAsyncData<CatalogSettings>(
    CATALOG_SETTINGS_KEY,
    () => api.settings.get<CatalogSettings>('catalog'))
}
