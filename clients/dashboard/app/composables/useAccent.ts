/**
 * The dashboard's accent colour: one of the presets in `assets/css/main.css`, picked in the
 * sidebar's Appearance menu.
 *
 * Per browser, in local storage, and deliberately not a DotNetBoost.Settings value: it is a
 * preference of whoever is looking at the dashboard, not a property of the store. The store's own
 * look — its name and logo — is the Branding group on the Settings page.
 *
 * `app.vue` puts it on `<html>` as `data-accent`; everything else only reads it.
 */
export const accents = ['neutral', 'violet', 'blue', 'green', 'orange', 'rose'] as const

export type Accent = typeof accents[number]

export function useAccent() {
  return useLocalStorage<Accent>('dashboard-accent', 'violet')
}
