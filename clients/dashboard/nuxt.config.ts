// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  modules: [
    '@nuxt/eslint',
    '@nuxt/ui',
    '@nuxtjs/i18n',
    '@vueuse/nuxt'
  ],

  // Client-rendered single-page app. Nitro is still used, but only to proxy the
  // API (see server/api/[...path].ts) — never to render pages.
  ssr: false,

  devtools: {
    enabled: true
  },

  css: ['~/assets/css/main.css'],

  runtimeConfig: {
    // Base address of the sample API: three libraries' endpoints plus the app's own.
    // Read by the proxy handler only, so the browser never sees it.
    // Override with NUXT_API_URL.
    apiUrl: 'http://localhost:5180',

    public: {
      // Linked from the sidebar — the API's own Scalar reference.
      apiReferenceUrl: 'http://localhost:5180/scalar',

      // MultiLingualOptions.BaseLanguage on the API, when base-language mode is on: the language
      // kept in the entities' own columns rather than in the translations table. This sample does
      // not use that mode, so it is empty and the Languages page never shows the "base" badge.
      // Override with NUXT_PUBLIC_BASE_LANGUAGE.
      baseLanguage: '',

      // Matches WithMaxFileSize(...) on the API. The upload form refuses a larger file before
      // sending it, so the user hears about it immediately rather than as a 413. The API
      // enforces it either way — this is courtesy, not a control.
      maxUploadBytes: 26214400
    }
  },

  compatibilityDate: '2026-06-30',

  eslint: {
    config: {
      stylistic: {
        commaDangle: 'never',
        braceStyle: '1tbs'
      }
    }
  },

  // Tajawal, which sets the Arabic — see `--font-sans` in app/assets/css/main.css for how it and
  // Public Sans divide the scripts between them.
  //
  // `@nuxt/fonts` comes with @nuxt/ui and already self-hosts whatever the CSS asks for: it fetches
  // the files at build time and serves them from this origin, which is why no <link> to
  // fonts.googleapis.com appears anywhere in this app. Adding one would fetch the same typeface a
  // second time, from a third party, on the critical path.
  //
  // Both settings below are here because the defaults are wrong for this font:
  //
  //   • it downloads weights 400 and 700 only, and the family is installed here with the full set
  //     Tajawal publishes. Only the weights a page actually draws IN ARABIC are ever sent to a
  //     browser, so the rest cost build time and disk, not load time;
  //   • its default subsets are the Latin, Cyrillic and Greek ones, and `arabic` is not among
  //     them. Without it the Arabic UI renders in a system fallback while the Latin one does not,
  //     which looks like a styling bug and is actually a missing download.
  //
  // `arabic` ALONE, and not because the Latin is unwanted: Public Sans is ahead of Tajawal in the
  // stack, so Tajawal's Latin faces could never be reached. Asking for them would put seven files
  // in the build that no browser can ever request.
  //
  // `global` is what makes the stack in main.css work at all, and it is not optional. @nuxt/fonts
  // reads only the FIRST family of a `font-family` declaration and treats the rest as fallbacks to
  // measure against local fonts — it does not fetch them. Tajawal is second there, so without this
  // it is never downloaded, and the Arabic silently renders in whatever the system offers. `global`
  // emits its @font-face rules anyway; `unicode-range` still keeps the files off the wire until
  // Arabic is actually on screen.
  //
  // Public Sans is not listed here at all. It is first in the stack, so @nuxt/fonts finds it in the
  // CSS by itself, and its defaults are right for it — a variable face covering the 400–700 the
  // dashboard draws.
  fonts: {
    families: [
      {
        name: 'Tajawal',
        provider: 'google',
        global: true,
        weights: [200, 300, 400, 500, 700, 800, 900],
        subsets: ['arabic']
      }
    ]
  },

  // The dashboard's own chrome, in the three languages the sample seeds.
  //
  // This is NOT the same mechanism as the content languages on the Languages page. Those are rows
  // in DotNetBoost.MultiLingual that the API resolves from `Accept-Language`; these are static
  // bundles compiled into the client. They are deliberately joined at one point only —
  // `plugins/ui-language.ts` moves this locale when the header's reading language moves — so that
  // picking Arabic translates the labels as well as the content.
  i18n: {
    // No `/ar/catalog`: the locale is a consequence of the header selector, not of the URL, so
    // there is nothing to prefix and nothing to redirect when it changes.
    strategy: 'no_prefix',
    defaultLocale: 'en',
    vueI18n: './i18n.config.ts',

    locales: [
      { code: 'en', language: 'en', name: 'English', dir: 'ltr', file: 'en.json' },
      { code: 'ar', language: 'ar', name: 'العربية', dir: 'rtl', file: 'ar.json' },
      { code: 'fr', language: 'fr', name: 'Français', dir: 'ltr', file: 'fr.json' }
    ],

    // The selector in the header is the only thing that chooses. Browser detection would set a
    // cookie that then disagrees with it on the next load, and the disagreement is invisible —
    // the labels would be in one language and the content in another with nothing to explain it.
    detectBrowserLanguage: false,

    experimental: {
      // Types `$t` against en.json, so a mistyped key fails `npm run typecheck` rather than
      // rendering its own name on the page.
      typedOptionsAndMessages: 'default'
    }
  }
})
