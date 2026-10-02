/**
 * The one place the dashboard's chrome is joined to the content it is showing.
 *
 * Two unrelated mechanisms meet here. The reading language is a row in DotNetBoost.MultiLingual
 * that the API resolves from `Accept-Language`, chosen by the selector in every page header; the
 * UI locale is a static bundle under `i18n/locales` compiled into the client. Nothing makes them
 * the same thing — this file decides that they move together, so picking Arabic in the header
 * translates the labels as well as the products.
 *
 * It is one watcher on purpose. The alternative, a second switcher for the chrome, is two controls
 * that can disagree, and the sample's whole point about the header selector is that one choice
 * carries everywhere.
 *
 * **A language with no bundle.** The reading languages are rows in a table that anyone can add to
 * on the Languages page; the UI locales are files in this repository. Adding `he` therefore gives
 * content in Hebrew and labels with nowhere to come from, and the honest answer is English rather
 * than `settings.form.saveChanges` rendered on a button. The shell still reads right-to-left,
 * because direction travels with the language row and not with the bundle.
 */
export default defineNuxtPlugin({
  name: 'ui-language',

  // vue-i18n has to exist before there is a locale to move, and the remembered language has to be
  // restored before this reads it — otherwise the first locale set here is the default one, and
  // the restore a moment later shows as a flicker of English chrome.
  dependsOn: ['i18n:plugin', 'reading-language'],

  setup(nuxtApp) {
    const i18n = nuxtApp.$i18n
    const language = useLanguage()

    const bundled = i18n.locales.value.map(locale =>
      typeof locale === 'string' ? locale : locale.code)

    /**
     * The bundle to show a reading language in: itself, else its base subtag, else the default.
     *
     * The base subtag is what makes `pt-BR` — which the Add-a-language form offers as an example —
     * read in Portuguese rather than in English the day someone adds a `pt.json`.
     *
     * Searched rather than looked up in a `Set`, so what comes back is one of the configured
     * locales to the type system as well as in fact, which is what `setLocale` asks for. Three
     * locales; the scan costs nothing.
     */
    function bundleFor(code: string) {
      const wanted = code.toLowerCase()
      const base = wanted.split('-')[0]

      return bundled.find(locale => locale === wanted)
        ?? bundled.find(locale => locale === base)
        ?? i18n.defaultLocale
    }

    watch(language, (code) => {
      const bundle = bundleFor(code)

      if (i18n.locale.value !== bundle) {
        i18n.setLocale(bundle)
      }
    }, { immediate: true })

    // `Intl` formatting — file sizes, dates, "3 hours ago" — follows the same locale, and so do
    // the handful of keys that are built at runtime rather than written out. Published rather than
    // imported because the helpers that need them are plain functions rather than composables.
    //
    // The cast is the price of `typedOptionsAndMessages`: `t` is typed to the keys in en.json,
    // which is exactly what is wanted at every call site in a component, and exactly what a helper
    // taking a runtime string cannot satisfy.
    type Key = Parameters<typeof i18n.t>[0]

    watch(i18n.locale, code => publishUiLocale(code, {
      // `String`, because a message whose value is a number comes back as one and `uiText`
      // promises its callers a string.
      t: key => String(i18n.t(key as Key)),
      // Asked of the default locale, not the current one. en.json is the source of truth for which
      // keys exist; a key present there and missing from ar.json resolves through `fallbackLocale`
      // and should count as present, or a half-written bundle would silently demote settings
      // fields to their humanised property names instead of showing English.
      te: key => i18n.te(key, i18n.defaultLocale)
    }), { immediate: true })
  }
})
