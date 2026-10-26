import { defineVitestConfig } from '@nuxt/test-utils/config'

/**
 * Unit tests for the dashboard's own logic.
 *
 * `environment: 'nuxt'` runs each file inside a Nuxt runtime, which is what makes the app's
 * auto-imported composables resolvable without importing them by path — the same way a component
 * uses them.
 *
 * They live under `test/nuxt/` because Nuxt's generated tsconfig already includes that path, so
 * `npm run typecheck` covers them. A spec anywhere else runs fine and is silently not typechecked,
 * which was true of this file for about ten minutes.
 *
 * There are no component-rendering tests here on purpose. What is worth guarding is the behaviour
 * of the composables the pages are assembled from; rendering shadcn-vue's components inside a test
 * harness asserts mostly that the harness works.
 */
export default defineVitestConfig({
  test: {
    environment: 'nuxt',
    include: ['test/**/*.spec.ts']
  }
})
