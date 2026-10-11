import { mockNuxtImport, mountSuspended, registerEndpoint } from '@nuxt/test-utils/runtime'
import { defineEventHandler, readBody } from 'h3'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import type { Language, NewLanguage } from '~/types'

/**
 * One language list, for every screen that shows one.
 *
 * <b>Why this file exists.</b> The dashboard read `GET api/languages` under four `useAsyncData`
 * keys and a hand-rolled fetch besides, and Nuxt neither de-duplicates across keys nor re-runs an
 * entry that has already resolved. So every copy was fetched separately and then frozen for the
 * rest of the session: adding a language on the Languages page left the header selector, the
 * catalogue's translation editors and the overview's count describing the languages as they had
 * been when each of those screens first loaded. The two properties below are what
 * `useLanguageList` exists to hold, and neither is visible from inside any one caller.
 */

// Reached on the way to the list and none of them under test. `useNotify` in particular would
// need a toast provider a bare runtime does not have.
mockNuxtImport('useToast', () => () => ({ add: vi.fn() }))
mockNuxtImport('useNotify', () => () => ({
  notifySuccess: vi.fn(), notifyFailure: vi.fn(), notifyProblem: vi.fn()
}))
mockNuxtImport('useI18n', () => () => ({ t: (key: string) => key }))

const english: Language = {
  code: 'en', name: 'English', direction: 'ltr', isDefault: true, isActive: true
}

/** The languages the API currently has. A POST appends, the way the real endpoint does. */
let serverLanguages: Language[]
/** How many times the list was actually asked for, which is half of what is under test. */
let reads: number

registerEndpoint('/api/languages', defineEventHandler(() => {
  reads++
  return serverLanguages
}))

registerEndpoint('/api/languages', {
  method: 'POST',
  handler: defineEventHandler(async (event) => {
    const body = await readBody<NewLanguage>(event)

    const created: Language = {
      code: body.code,
      name: body.name,
      direction: body.direction ?? 'ltr',
      isDefault: false,
      isActive: true
    }

    serverLanguages = [...serverLanguages, created]

    return created
  })
})

/**
 * Three independent readers, mounted the way the dashboard mounts them: the shell's selector and
 * a page both want the list, the Languages page also writes it, and no one of them knows the
 * others exist.
 */
async function mountReaders() {
  let shell!: ReturnType<typeof useLanguageList>
  let page!: ReturnType<typeof useLanguageList>
  let admin!: ReturnType<typeof useLanguages>

  const reader = (assign: (api: ReturnType<typeof useLanguageList>) => void) =>
    defineComponent({
      setup() {
        assign(useLanguageList())
        return () => h('div')
      }
    })

  await mountSuspended(reader(api => shell = api))
  await mountSuspended(reader(api => page = api))
  await mountSuspended(defineComponent({
    setup() {
      admin = useLanguages()
      return () => h('div')
    }
  }))

  // The entry is registered during setup and fetched right after, so mounting does not mean the
  // list has landed.
  await vi.waitFor(() => expect(shell.loaded.value).toBe(true))

  return { shell, page, admin }
}

beforeEach(() => {
  serverLanguages = [english]
  reads = 0
})

describe('useLanguageList', () => {
  it('is one request for every reader, and one write reaches all of them', async () => {
    const { shell, page, admin } = await mountReaders()

    // Three readers, one `useAsyncData` key: the second and third find the entry resolved and
    // read it rather than asking again. This was three requests and three answers that could
    // then drift apart.
    expect(reads).toBe(1)
    expect(shell.languages.value).toHaveLength(1)
    expect(page.languages.value).toHaveLength(1)

    // A language added on the Languages page — Hebrew, which reads right to left, because the
    // shell's direction comes from this row and nowhere else.
    const created = await admin.create({ code: 'he', name: 'עברית', direction: 'rtl' })

    // Guards the test itself: `notifyFailure` is stubbed, so a refused POST would otherwise look
    // like a passing assertion below.
    expect(created?.code).toBe('he')

    // The write refreshed the one entry, so the readers that never asked again are current.
    expect(reads).toBe(2)
    expect(shell.languages.value.map(l => l.code)).toContain('he')
    expect(page.languages.value.map(l => l.code)).toContain('he')
    expect(shell.languages.value.find(l => l.code === 'he')?.direction).toBe('rtl')
  })
})
