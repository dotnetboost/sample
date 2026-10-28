import { fileURLToPath } from 'node:url'
import { defineConfig, devices } from '@playwright/test'
import type { ConfigOptions } from '@nuxt/test-utils/playwright'

/**
 * The browser smoke test — `npm run test:e2e`. Separate from `npm test`, which is Vitest over the
 * composables and needs no browser.
 *
 * `@nuxt/test-utils/playwright` starts the dashboard itself, once per worker, and points
 * `baseURL` at it. The API is never started: `test/e2e/api.ts` answers every `/api/**` call from
 * the browser.
 *
 * `*.e2e.ts` rather than `*.spec.ts`, so `vitest.config.ts` — which takes every `test/**\/*.spec.ts`
 * — cannot pick these up and try to run a browser test inside its Nuxt environment.
 */
export default defineConfig<ConfigOptions>({
  testDir: './test/e2e',
  testMatch: '*.e2e.ts',

  // One worker, because each one starts its own Nuxt — and one is enough for a dozen page loads.
  workers: 1,
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['list']] : 'list',

  use: {
    ...devices['Desktop Chrome'],
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    nuxt: {
      rootDir: fileURLToPath(new URL('.', import.meta.url)),
      // The dev server, not a production build: Vue only warns in development, and a warning —
      // "Failed to resolve component" — is half of what the page tests are looking for.
      dev: true
    }
  }
})
