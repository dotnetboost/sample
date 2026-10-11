import { ar, en, fr } from '@nuxt/ui/locale'

/**
 * What the shell needs to know about the language it is being read in: which way it reads, and
 * which of Nuxt UI's own string bundles to hand the component library.
 *
 * **Direction is the API's answer.** `GET api/languages` stores it per language, so adding `he` on
 * the Languages page comes back right-to-left without this file being told, and there is no
 * hardcoded list of RTL codes in the client to forget to update. That is the same reason `isRtl`
 * exists rather than a comparison written out at each call site.
 *
 * **One list, every reader.** `app.vue`, the layout and the header selector all want the
 * languages, and so do the overview, the catalogue and the alt-text editor: they share the one
 * `useLanguageList` entry, which is both a single request and — the part that was missing — a
 * single thing for a write on the Languages page to refresh.
 */

/**
 * Nuxt UI's own labels — the pagination buttons, a dialog's close button, the file dropzone — for
 * the locales this dashboard ships. Imported one by one rather than as the whole barrel, which
 * would pull some fifty bundles into the client to use three.
 */
const componentLocales = { en, ar, fr }

export function useShellLocale() {
  const language = useLanguage()
  const { locale } = useI18n()

  const { languages, loaded } = useLanguageList()

  // Read once, at setup: what the API said about this language the last time it was asked.
  const remembered = readRememberedLanguage()

  const direction = computed<'ltr' | 'rtl'>(() => {
    const live = languages.value.find(l => l.code === language.value)?.direction

    if (live !== undefined) {
      return isRtl(live) ? 'rtl' : 'ltr'
    }

    // The list is still in flight. Opening in Arabic and laying it out left-to-right for a round
    // trip, then jumping, is worse than trusting the answer we were given last time — and the live
    // answer replaces it the moment it lands, so a direction changed on the Languages page is
    // never wrong for longer than that.
    return remembered?.code === language.value ? remembered.direction : 'ltr'
  })

  // Keyed off the UI locale rather than the reading language, so the fallback for a language with
  // no bundle is decided in exactly one place — `plugins/ui-language.ts` — and Nuxt UI's strings
  // cannot end up in a different language from the dashboard's own.
  const componentLocale = computed(() =>
    componentLocales[locale.value as keyof typeof componentLocales] ?? componentLocales.en)

  return { languages, loaded, direction, componentLocale }
}
