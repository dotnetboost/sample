import type { Page } from '@playwright/test'
import { expect, test } from '@nuxt/test-utils/playwright'
import { stubApi } from './api'

/**
 * The dashboard in a real browser, against a stubbed API.
 *
 * This exists because of a regression nothing else could see. The move to shadcn-vue dropped
 * vue-sonner's stylesheet, so every toast was raised, mounted and drawn into an unpositioned list
 * nobody could see — and lint, the typecheck and the composable tests all passed, because none of
 * them has a viewport. So this suite asserts what a reader would notice: that each page draws its
 * title without a console error or a Vue warning, that a toast has a box on screen, that a tooltip
 * opens, and that Arabic turns the page around.
 *
 * Smoke, not behaviour: what the pages DO with their data is the composables' business and is
 * tested under `test/nuxt/`. Nothing here needs the .NET API — see `api.ts`.
 */

interface Watched {
  /** Console errors, uncaught exceptions, and every `[Vue warn]` — a warning that is a bug. */
  problems: string[]
  /** API calls the stubs did not answer. */
  unhandled: string[]
  /** Console errors a test causes on purpose. */
  expected: RegExp[]
}

const watched = new WeakMap<Page, Watched>()

/** Lets one console error through — for a test that provokes it. Anything else still fails. */
function expectConsoleError(page: Page, pattern: RegExp) {
  watched.get(page)!.expected.push(pattern)
}

test.beforeEach(async ({ page }) => {
  const problems: string[] = []

  page.on('console', (message) => {
    const text = message.text()

    if (message.type() === 'error' || text.includes('[Vue warn]')) {
      problems.push(`${message.type()}: ${text}`)
    }
  })
  page.on('pageerror', error => problems.push(`pageerror: ${error.message}`))

  watched.set(page, { problems, unhandled: await stubApi(page), expected: [] })
})

test.afterEach(({ page }) => {
  const { problems, unhandled, expected } = watched.get(page)!

  expect(unhandled, 'API calls with no stub in test/e2e/api.ts').toEqual([])
  expect(problems.filter(problem => !expected.some(pattern => pattern.test(problem))),
    'console errors and Vue warnings').toEqual([])
})

/** The page's own `<h1>` — the one in `DashboardPage`'s header. */
const heading = (page: Page) => page.getByRole('heading', { level: 1 })

const pages = [
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

for (const { path, title } of pages) {
  test(`${path} renders its title`, async ({ page, goto }) => {
    await goto(path)

    await expect(heading(page)).toHaveText(title)
    await expect(page).toHaveTitle(`${title} — DotNetBoost Sample`)
    // Settled: nothing still loading behind the title that could log once the test has ended.
    await expect(page.locator('[data-slot="skeleton"]')).toHaveCount(0)
  })
}

test('a toast is on screen, not just in the DOM', async ({ page, goto }) => {
  // The default language cannot be switched off: the API answers with problem details, which is
  // the refusal toast every write in the dashboard falls back to.
  await page.route('**/api/languages/en', route => route.fulfill({
    status: 400,
    contentType: 'application/problem+json',
    json: { title: 'Bad Request', status: 400, detail: 'The default language cannot be deactivated.' }
  }))

  // The browser logs the refusal as a failed resource; that one is the point of the test.
  expectConsoleError(page, /status of 400/)

  await goto('/languages')
  await page.getByRole('switch', { name: 'Deactivate English' }).click()

  const toast = page.locator('[data-sonner-toast]')
  await expect(toast).toContainText('The default language cannot be deactivated.')

  // Wholly inside the viewport, once it has slid in — `toBeInViewport` retries through the
  // animation. Without vue-sonner's stylesheet the toast is in the DOM all the same, but as an
  // item in an unpositioned list at the foot of the page.
  await expect(toast).toBeInViewport({ ratio: 1 })

  const box = await toast.boundingBox()
  expect(box?.width, 'the toast has a width').toBeGreaterThan(0)
  expect(box?.height, 'the toast has a height').toBeGreaterThan(0)
})

test('a tooltip opens on hover', async ({ page, goto }) => {
  await goto('/audit')

  // The first row's "3 hours ago", whose tooltip is the exact instant.
  const firstRow = page.getByRole('row').nth(1)
  await firstRow.getByText(/hours? ago/).hover()

  const tooltip = page.locator('[data-slot="tooltip-content"]')
  await expect(tooltip).toBeVisible()
  await expect(tooltip).toBeInViewport()
})

test('Arabic turns the page right-to-left', async ({ page, goto }) => {
  await goto('/')
  await expect(page.locator('html')).toHaveAttribute('dir', 'ltr')

  await page.getByRole('combobox', { name: 'Reading language' }).click()
  await page.getByRole('option', { name: /العربية/ }).click()

  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl')
  await expect(page.locator('html')).toHaveAttribute('lang', 'ar')
  await expect(heading(page)).toHaveText('نظرة عامة')
})
