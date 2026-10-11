import type { Language } from '~/types'

/**
 * The key every reader of the content languages shares, so the dashboard asks for them once and
 * one refresh reaches every screen showing them.
 */
export const LANGUAGES_KEY = 'languages'

/**
 * The content languages from `GET api/languages`, read once for the whole dashboard.
 *
 * One `useAsyncData` key is what makes that true. A second caller finds the entry already
 * resolved and reads it instead of asking again, and `refresh()` — which every write in
 * `useLanguages` ends with — updates all of them at once.
 *
 * There used to be five readers of this one endpoint and no two of them shared anything: four
 * `useAsyncData` keys (`shell-languages`, `overview-languages`, `catalog-languages`,
 * `alt-text-languages`) and a hand-rolled fetch inside `useLanguages`. Nuxt only de-duplicates by
 * key and only refetches a resolved entry when something asks it to, so each copy was fetched
 * separately and then kept whatever it had first seen for the rest of the session: a language
 * added on the Languages page did not exist to the header selector, the catalogue's translation
 * editors or the overview's count until a full page reload. The shell reading right-to-left for a
 * newly added RTL language — which the README promises — needs this list to be current, because
 * direction travels with the language row.
 *
 * The read-only twin of `useLanguages`, which is this plus the administration calls — the same
 * split as `useCatalogSettings` beside `useSettingsGroup`. The shell, the overview and the
 * alt-text editor want the list; none of them has any business holding a `remove`.
 */
export function useLanguageList() {
  const api = useApi()
  const { baseLanguage } = useRuntimeConfig().public

  // Abortable, which is what makes Nuxt's own cancellation reach the network: it aborts the
  // handler's signal when the entry is refreshed while in flight and when the last reader
  // unmounts, and without the signal being passed on, the request itself runs to completion
  // anyway and its answer is merely discarded.
  const asyncData = useAsyncData<Language[]>(
    LANGUAGES_KEY,
    (_nuxtApp, { signal }) => api.multiLingual.languages({ signal }),
    // `defer` is what makes "one key" mean one request. Readers register in the same tick —
    // `app.vue`, the layout and the header selector, then whichever page is opening — and under
    // the default `cancel` each arrival aborts the request in flight and issues its own, so three
    // readers meant three requests and two cancellations for one answer. Deferring hands every
    // later reader the pending promise instead.
    //
    // Safe here in a way it would not be for `useBranding`, which re-reads on the reading
    // language and must never be handed an answer already in flight for the previous one: this
    // list sends no `Accept-Language` and does not change with it. A write still forces a fresh
    // read — see `refresh({ dedupe: 'cancel' })` in `useLanguages`.
    { dedupe: 'defer' })

  const { data, status, pending, error, refresh } = asyncData

  /** Never null — a caller filters it. Whether it has answered yet is `loaded`. */
  const languages = computed(() => data.value ?? [])

  const active = computed(() => languages.value.filter(language => language.isActive))
  const defaultLanguage = computed(() => languages.value.find(language => language.isDefault))

  /**
   * Whether the list has answered, which an empty list cannot say on its own.
   *
   * `app.vue` needs the difference: it remembers the language's direction beside the language, and
   * a reload must not overwrite a known direction with the `ltr` that stands in while the request
   * is still out.
   */
  const loaded = computed(() => status.value === 'success')

  /**
   * A refusal in the API's own words. The problem detail is already written for the caller, so it
   * is read out of the failure rather than replaced with something vaguer here.
   */
  const loadError = computed(() => error.value ? problemMessage(error.value) : '')

  function find(code: string) {
    return languages.value.find(language => language.code.toLowerCase() === code.toLowerCase())
  }

  /**
   * Whether `code` is `MultiLingualOptions.BaseLanguage` — the language kept in the entity's own
   * columns rather than in the translations table.
   *
   * This sample does not configure one, so it is always false and the badge never shows. Left in
   * because the mode exists and the page would be wrong without it the moment someone turns it on.
   */
  function isBase(code: string) {
    return !!baseLanguage && code.toLowerCase() === String(baseLanguage).toLowerCase()
  }

  /**
   * Resolves once the list has answered, for a caller that reads it a single time rather than
   * watching it — the alt-text editor draws a row per language, and a row per language of a list
   * that has not landed yet is no rows at all.
   *
   * This is `useAsyncData`'s own promise, so the first caller awaits the initial request and every
   * later one continues immediately.
   */
  const ready = async () => {
    await asyncData
    return languages.value
  }

  return {
    languages, active, defaultLanguage, loaded, pending, loadError,
    find, isBase, ready, refresh
  }
}
