/**
 * What the shell needs to know about the language it is being read in: which way it reads.
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

export function useShellLocale() {
  const language = useLanguage()

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

  return { languages, loaded, direction }
}
