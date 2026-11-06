import type { Page } from '@playwright/test'
import { expect, test as nuxtTest } from '@nuxt/test-utils/playwright'
import { stubApi } from './api'

/**
 * Every page of the dashboard, with the title its header draws. The smoke test visits each one, and
 * the warm-up below visits each one first.
 */
export const pages = [
  { path: '/', title: 'Overview' },
  { path: '/catalog', title: 'Catalog' },
  { path: '/catalog?tab=products', title: 'Catalog' },
  { path: '/media', title: 'Media' },
  { path: '/languages', title: 'Languages' },
  { path: '/audit', title: 'Audit' },
  { path: '/settings/branding', title: 'Settings' },
  { path: '/settings/catalog', title: 'Settings' },
  { path: '/settings/auditing', title: 'Settings' }
]

/** The page's own `<h1>` — the one in `DashboardPage`'s header. */
export const heading = (page: Page) => page.getByRole('heading', { level: 1 })

/** Long enough for a cold page on a slow runner; it bounds the warm-up only, never a test. */
const COLD_PAGE_TIMEOUT = 60_000

/**
 * `@nuxt/test-utils/playwright`'s `test`, with every page visited once before any test runs.
 *
 * The suite runs against the dev server (Vue only warns in development), and the dev server
 * compiles a module the first time a browser asks for it. A page's first visit therefore pays for
 * the few hundred modules under it, one import at a time. On a CI runner that is longer than the
 * five seconds an assertion waits, so the first test of each page failed with the page still on
 * its loading screen.
 *
 * And it never got better, because a failed test makes Playwright replace its worker, which starts
 * a NEW Nuxt — and Vite, finding its config changed, re-optimizes and starts cold again. One slow
 * page was every page failing, twice.
 *
 * So the worker pays for the compiling up front: once its Nuxt is up, this opens each page in a
 * browser of its own and waits for the title. It asserts nothing. It only leaves Vite with every
 * module compiled, so the tests' own five-second waits measure the page and not the compiler. A
 * page that never draws its title here is left for its test to report, with that test's trace.
 */
export const test = nuxtTest.extend<object, Record<'warmUp', void>>({
  warmUp: [async ({ _nuxtHooks, browser }, use) => {
    const context = await browser.newContext({ baseURL: _nuxtHooks.ctx.url })
    const page = await context.newPage()
    await stubApi(page)

    for (const { path } of pages) {
      try {
        await page.goto(path)
        await expect(heading(page)).toBeVisible({ timeout: COLD_PAGE_TIMEOUT })
        await expect(page.locator('[data-slot="skeleton"]')).toHaveCount(0, { timeout: COLD_PAGE_TIMEOUT })
      } catch (error) {
        console.warn(`warm-up: ${path} did not settle; its test will say why.`, error)
      }
    }

    await context.close()
    await use()
  }, { scope: 'worker', auto: true, timeout: 10 * 60_000 }]
})

export { expect }
