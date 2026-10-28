import { Palette } from '@lucide/vue'
import { mockNuxtImport, mountSuspended, registerEndpoint } from '@nuxt/test-utils/runtime'
import { defineEventHandler, setResponseHeader } from 'h3'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, ref } from 'vue'
import type { SettingGroup, SettingValues } from '~/types'

/**
 * What a settings group re-reads, and when.
 *
 * <b>Why this file exists.</b> A browser caught a bug nothing else could: attaching a logo on the
 * branding tab left the role box showing the file that had just been replaced. The attach landed,
 * the header updated, and the box did not — because the images it draws come from this
 * composable's `values`, a plain ref, while the refresh being called could only re-run
 * `useAsyncData` entries. The fix is `refreshValues()`, and these are the two properties it has to
 * hold at once.
 */

// The composable reaches for three app-wide composables on the way to doing its job. None of them
// is what is under test, and all three need a provider a bare runtime does not have — so they are
// stubbed down to the shape it actually uses. (Its one direct toast, the save conflict, is
// vue-sonner's `toast`, which needs no provider: with no `<Toaster>` mounted it is simply unseen.)
mockNuxtImport('useNotify', () => () => ({
  notifySuccess: vi.fn(), notifyFailure: vi.fn(), notifyProblem: vi.fn()
}))
mockNuxtImport('useI18n', () => () => ({ t: (key: string) => key }))
mockNuxtImport('useLanguage', () => () => ref('en'))

const group: SettingGroup = {
  route: 'branding',
  name: 'BrandingSettings',
  requiresAuthorization: false,
  // Not from the API — the dashboard's own, which is why the type carries it.
  icon: Palette
}

/** The properties the schema reports, which is what the form draws boxes for. */
const propertyNames = ['storeName', 'accentColor']

/** What the next `GET api/settings/branding` answers with. Reassigned by a test to move the
 *  server underneath the form, which is the whole subject here. */
let serverState: SettingValues

/**
 * The group's endpoint, answering out of `serverState`.
 *
 * Through `registerEndpoint` rather than a stubbed global: Nuxt's test runtime provides its own
 * `$fetch`, so `vi.stubGlobal('$fetch', …)` is simply not the function the composable calls — it
 * answered 404 from Nuxt's own server instead. This registers a real handler on that server, which
 * also means the ETag travels as a header the way the composable expects to read it.
 */
registerEndpoint('/api/settings/branding', defineEventHandler((event) => {
  setResponseHeader(event, 'etag', '"revision-1"')
  return serverState
}))

/**
 * Mounts something that uses the composable, so `onMounted(load)` runs and the first read lands
 * before any assertion — the same order a page gets.
 */
async function loadGroup() {
  let api!: ReturnType<typeof useSettingsGroup>

  const Host = defineComponent({
    setup() {
      api = useSettingsGroup(group, ref(propertyNames))
      return () => h('div')
    }
  })

  await mountSuspended(Host)

  // `onMounted(load)` is fire-and-forget, so mounting does not mean the first read has landed.
  // Without this the tests raced it: an edit made here was overwritten moments later by the load
  // still in flight — which looks exactly like the bug under test and is not it.
  await vi.waitFor(() => expect(api.pending.value).toBe(false))

  return api
}

beforeEach(() => {
  serverState = {
    storeName: 'Stored name',
    accentColor: '#512BD4',
    // Not settings, and the reason any of this matters: they are part of the response the
    // projector shapes, and they change through requests this form never makes.
    images: [{ fileName: 'old.png' }],
    owner: { entityName: 'Branding', entityId: 'branding' }
  } as unknown as SettingValues
})

describe('refreshValues', () => {
  it('picks up media attached outside the form', async () => {
    const api = await loadGroup()

    expect((api.values.value.images as unknown as { fileName: string }[])[0]!.fileName)
      .toBe('old.png')

    // An attach happened somewhere else entirely — the media library's own endpoint, from the
    // box below the form.
    serverState = { ...serverState, images: [{ fileName: 'new.png' }] } as unknown as SettingValues

    await api.refreshValues()

    expect((api.values.value.images as unknown as { fileName: string }[])[0]!.fileName)
      .toBe('new.png')
  })

  it('keeps a field the user is part-way through editing, and adopts the rest', async () => {
    const api = await loadGroup()

    // Typed, not saved. An attach must not throw this away: it is a different request, about a
    // different thing, and the user never asked for their edit to be discarded.
    api.state.storeName = 'Half-typed edit'

    // Meanwhile the server moved on in a field the user has NOT touched.
    serverState = { ...serverState, accentColor: '#000000' } as unknown as SettingValues

    await api.refreshValues()

    expect(api.state.storeName).toBe('Half-typed edit')
    expect(api.state.accentColor).toBe('#000000')
  })

  it('is not load(), which is allowed to discard edits', async () => {
    const api = await loadGroup()

    api.state.storeName = 'Half-typed edit'

    // The contrast that explains why refreshValues exists at all. `load()` is the page asking for
    // the group again from scratch; it takes the server's answer whole, edits and all.
    await api.load()

    expect(api.state.storeName).toBe('Stored name')
  })
})
