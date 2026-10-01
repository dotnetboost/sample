// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  modules: [
    '@nuxt/eslint',
    '@nuxt/ui',
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
  }
})
