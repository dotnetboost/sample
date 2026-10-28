// https://nuxt.com/docs/api/configuration/nuxt-config
import tailwindcss from '@tailwindcss/vite'

export default defineNuxtConfig({
  modules: [
    '@nuxt/eslint',
    '@nuxt/fonts',
    '@nuxtjs/color-mode',
    '@nuxtjs/i18n',
    '@vueuse/nuxt',
    'shadcn-nuxt'
  ],

  // Client-rendered single-page app. Nitro is still used, but only to proxy the
  // API (see server/api/[...path].ts) — never to render pages.
  ssr: false,

  devtools: {
    enabled: true
  },

  // The document that `ssr: false` serves is a shell with no page in it, and so with none of the
  // head a page would have contributed. Every page does set a title — `nav.documentTitle`, in the
  // reading language — but not until Vue is running, so until then the tab reads as the URL, and a
  // bookmark or a shared link made in that moment keeps it. These two are the shell's own answer
  // for the gap; `app.vue` replaces both the moment it mounts, with the language actually being
  // read rather than the one the bundle was built with.
  //
  // See also `app/spa-loading-template.html`, which is what fills the page itself in that gap.
  app: {
    head: {
      title: 'DotNetBoost Sample',
      htmlAttrs: { lang: 'en' }
    }
  },

  // vue-sonner 2 no longer injects its own styles: without its stylesheet the `<Toaster>` in
  // app.vue mounts, and every toast is drawn into an unpositioned list nobody sees.
  css: ['vue-sonner/style.css', '~/assets/css/main.css'],

  // shadcn-vue's dark theme hangs off a bare `.dark` class on `<html>`, not `.dark-mode`.
  colorMode: {
    classSuffix: ''
  },

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

  vite: {
    plugins: [tailwindcss()],

    // Pre-bundled when the dev server starts, rather than discovered on the first page that imports
    // them. Discovered late, each one re-bundles the lot and reloads the browser mid-page — a
    // flicker in development, and a `504 (Outdated Optimize Dep)` with a failed import in the
    // browser smoke test (`npm run test:e2e`), which runs against the dev server and starts cold.
    // The list is the one `nuxt dev` prints when it finds them; a new dependency imported from a
    // page belongs here too, and the smoke test is what says so.
    optimizeDeps: {
      include: [
        '@internationalized/date',
        '@lucide/vue',
        '@tanstack/vue-table',
        '@unovis/vue',
        '@vue/devtools-core',
        'class-variance-authority',
        'cn',
        'reka-ui',
        'vue-sonner'
      ]
    }
  },

  // The browser smoke test is Node code — Playwright's runner and its stubs — so it is typechecked
  // in the Node context, beside this file. Nothing else would include it: Nuxt's generated configs
  // cover `test/nuxt/` and not `test/e2e/`.
  typescript: {
    nodeTsConfig: {
      include: ['../playwright.config.ts', '../test/e2e/**/*']
    }
  },

  eslint: {
    config: {
      stylistic: {
        commaDangle: 'never',
        braceStyle: '1tbs'
      }
    }
  },

  // Geist, the face shadcn-vue's blocks use, and Tajawal, which sets the Arabic —
  // see `--font-sans` in app/assets/css/main.css for how it and Geist divide the scripts between
  // them.
  //
  // `@nuxt/fonts` self-hosts whatever the CSS asks for: it fetches
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
  // `arabic` ALONE, and not because the Latin is unwanted: Geist is ahead of Tajawal in the
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
  // `fallbacks: []` on Geist is the other half, and it is just as load-bearing. By default
  // @nuxt/fonts rewrites the declaration to insert metric-matched local faces straight after the
  // FIRST family, so the stack the browser actually gets reads
  //
  //   Geist, …Fallback: BlinkMacSystemFont, …Segoe UI, …Helvetica Neue, …Arial, …Noto Sans,
  //   Tajawal, sans-serif
  //
  // Those are whole system fonts, and Arial carries Arabic. Arabic therefore matched Arial and
  // never reached Tajawal — the font looked like it had simply not been applied. Emptying the list
  // stops the rewrite, which costs the no-layout-shift overrides while Geist loads and buys
  // back the only thing that makes two scripts in one stack work.
  //
  // Geist is listed for that one setting. Everything else about it stays default: it is first in
  // the stack, so @nuxt/fonts finds it in the CSS and fetches the variable face covering the
  // 400–700 the dashboard draws.

  fonts: {
    families: [
      {
        name: 'Geist',
        provider: 'google',
        fallbacks: []
      },
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
  },

  // The components under `components/ui` are shadcn-vue's, copied into this repository by its CLI
  // (`npx shadcn-vue@latest add <name>`) rather than installed as a package — they are ours to read
  // and change. No prefix, so they are `<Button>` and `<Card>`, the names the shadcn-vue docs and
  // blocks use, and a block pasted from there works without a rename.
  shadcn: {
    prefix: '',
    componentDir: '@/components/ui'
  }
})
