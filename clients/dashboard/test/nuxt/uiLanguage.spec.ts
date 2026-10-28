import { mountSuspended, registerEndpoint } from '@nuxt/test-utils/runtime'
import { defineEventHandler } from 'h3'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import type { Language } from '~/types'

/**
 * One selector, both languages.
 *
 * <b>Why this file exists.</b> The README's claim about the header is that picking a language
 * moves the content AND the chrome, and that a language with no bundle still reads the right way
 * round. Two unrelated mechanisms have to agree for that — the reading language, a MultiLingual
 * row, and the UI locale, a static bundle — and `plugins/ui-language.ts` is the only thing joining
 * them. Nothing else would notice if it stopped: every page still renders, just with English
 * labels around Arabic products.
 *
 * Nothing is mocked here. The Nuxt test runtime runs the app's own plugins, so setting
 * `useLanguage()` is exactly what the header selector does, and the assertions read the same
 * `$i18n`, `uiText` and `intlLocale` the pages do.
 */

const languages: Language[] = [
  { code: 'en', name: 'English', direction: 'ltr', isDefault: true, isActive: true },
  { code: 'ar', name: 'العربية', direction: 'rtl', isDefault: false, isActive: true },
  // Added on the Languages page, so it has content and no bundle: there is no he.json.
  { code: 'he', name: 'עברית', direction: 'rtl', isDefault: false, isActive: true }
]

registerEndpoint('/api/languages', defineEventHandler(() => languages))

/** Picks a reading language the way the header does, and waits for the bundle to load. */
async function read(code: string, expectedLocale: string) {
  useLanguage().value = code

  // `setLocale` loads the bundle lazily, so the locale lands a tick or two after the choice.
  await vi.waitFor(() => expect(useNuxtApp().$i18n.locale.value).toBe(expectedLocale))
}

afterEach(async () => {
  // One Nuxt app for the whole file: put it back so no test starts in another's language.
  await read('en', 'en')
})

describe('the reading-language selector', () => {
  it('moves the labels with the content', async () => {
    await read('ar', 'ar')

    // The runtime-key lookup and `Intl` follow too, not just `$t` — a file size on the same card
    // as an Arabic label has to be Arabic as well, in Latin digits.
    expect(uiText('nav.catalog', 'Catalog')).toBe('الكتالوج')
    expect(intlLocale()).toBe('ar-u-nu-latn')

    await read('fr', 'fr')

    expect(uiText('nav.catalog', 'Catalog')).toBe('Catalogue')
  })

  it('reads a regional code in its base language\'s bundle', async () => {
    await read('fr-CA', 'fr')
  })

  it('shows a language with no bundle in English, laid out right to left', async () => {
    let shell!: ReturnType<typeof useShellLocale>

    await mountSuspended(defineComponent({
      setup() {
        shell = useShellLocale()
        return () => h('div')
      }
    }))

    await vi.waitFor(() => expect(shell.loaded.value).toBe(true))

    // From Arabic, because that is where the fallback matters: nuxt-i18n ignores a locale it has
    // no bundle for, so without one the labels would stay Arabic around Hebrew content.
    await read('ar', 'ar')
    await read('he', 'en')

    // Labels from en.json rather than their own keys…
    expect(uiText('nav.catalog', 'fallback')).toBe('Catalog')

    // …and the shell still flips, because direction comes from the language row, not the bundle.
    expect(shell.direction.value).toBe('rtl')
  })
})
