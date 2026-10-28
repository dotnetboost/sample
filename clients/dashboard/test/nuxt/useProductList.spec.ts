import { mountSuspended, registerEndpoint } from '@nuxt/test-utils/runtime'
import { enableAutoUnmount } from '@vue/test-utils'
import { defineEventHandler, getHeader, getQuery } from 'h3'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import type { Product } from '~/types'

/**
 * The question the Products tab asks, and how it changes.
 *
 * <b>Why this file exists.</b> This state lived in the Catalog page until the page was split, and
 * none of it shows in a screenshot. The page size is the API's, from a setting, so the pager has to
 * be drawn from what the API says it used. A search is typed a key at a time and must not be a
 * request per key. A filter that changes leaves the old page number pointing past its end, and so
 * does a delete that empties the last page. And a language switch has to re-read the same rows.
 */

/** What the fake API serves when the caller sends no page size — `CatalogSettings.PageSize`. */
const SETTING_PAGE_SIZE = 2

const product = (id: number, name: string, sku: string, categoryId: number): Product => ({
  id, name, description: '', sku, price: 1, categoryId, primary: null, images: []
})

const CATALOGUE: Product[] = [
  product(1, 'Oak writing desk', 'DESK-001', 1),
  product(2, 'Walnut chair', 'CHAIR-001', 1),
  product(3, 'Brass desk lamp', 'LAMP-001', 2),
  product(4, 'Standing desk', 'DESK-002', 1),
  product(5, 'Floor lamp', 'LAMP-002', 2)
]

/** The products the API holds now. A test deletes from it. */
let stored: Product[]
/** Every query the listing endpoint was asked, with the language it was asked in. */
let reads: (Record<string, string | undefined> & { language?: string })[]

/**
 * `GET api/products`, close enough to the real one where it matters: no `pageSize` means the
 * setting's, it answers with the size it used, and it filters by search and category before paging.
 */
registerEndpoint('/api/products', defineEventHandler((event) => {
  const query = getQuery(event) as Record<string, string | undefined>
  reads.push({ ...query, language: getHeader(event, 'accept-language') })

  const take = Number(query.pageSize ?? SETTING_PAGE_SIZE)
  const term = query.search?.toLowerCase()

  const matching = stored.filter(p =>
    (!term || p.name.toLowerCase().includes(term) || p.sku.toLowerCase().includes(term))
    && (!query.categoryId || p.categoryId === Number(query.categoryId)))

  const skip = (Math.max(Number(query.page ?? 1), 1) - 1) * take

  return { items: matching.slice(skip, skip + take), total: matching.length, skip, take }
}))

/** Mounts something that reads the listing, the way the Products tab does: awaited. */
async function mountList() {
  let list!: Awaited<ReturnType<typeof useProductList>>

  await mountSuspended(defineComponent({
    async setup() {
      list = await useProductList()

      return () => h('div')
    }
  }))

  return list
}

// One Nuxt app for the whole file: unmount the previous reader, and clear its resolved entry, or
// the next test is answered by the last one's page.
enableAutoUnmount(afterEach)

beforeEach(async () => {
  stored = [...CATALOGUE]
  reads = []

  await mountSuspended(defineComponent({
    setup() {
      clearNuxtData(PRODUCT_LIST_KEY)
      useLanguage().value = 'en'

      return () => h('div')
    }
  }))
})

describe('useProductList', () => {
  it('leaves the page size to the API, and draws the pager from what it used', async () => {
    const list = await mountList()

    // Awaited, so the first page is already there.
    expect(list.products.value.map(p => p.id)).toEqual([1, 2])

    // Nothing sent about size: a size decided here would be a second answer to the setting's.
    expect(reads.at(-1)?.pageSize).toBeUndefined()

    expect(list.pageSize.value).toBe(SETTING_PAGE_SIZE)
    expect(list.pageCount.value).toBe(3)
    expect(list.range.value).toBe('1–2 of 5')

    list.page.value = 3

    await vi.waitFor(() => expect(list.products.value.map(p => p.id)).toEqual([5]))
    expect(list.range.value).toBe('5–5 of 5')
  })

  it('sends a search once typing stops, and goes back to page 1 for it', async () => {
    const list = await mountList()

    list.page.value = 2
    await vi.waitFor(() => expect(reads.at(-1)?.page).toBe('2'))
    const before = reads.length

    // Four keystrokes, each in a tick of its own as typing is, and closer together than the
    // debounce. In one tick Vue would merge them anyway, and the debounce would go untested.
    for (const typed of ['d', 'de', 'des', 'desk']) {
      list.searchBox.value = typed
      await new Promise(resolve => setTimeout(resolve, 50))
    }

    await vi.waitFor(() => expect(reads.at(-1)?.search).toBe('desk'))

    // One request for the whole word, not one per key, and on page 1: page 2 of everything is
    // past the end of three desks at two a page only by luck.
    expect(reads.slice(before).map(read => read.search)).toEqual(['desk'])
    expect(list.page.value).toBe(1)
    expect(list.isFiltered.value).toBe(true)
    expect(list.products.value.map(p => p.id)).toEqual([1, 3])
  })

  it('filters by category, and clears back to the whole listing', async () => {
    const list = await mountList()

    list.page.value = 2
    await vi.waitFor(() => expect(reads.at(-1)?.page).toBe('2'))

    // A category with two pages of its own, so landing on page 1 is the reset and not the
    // last-page clamp doing the same thing by coincidence.
    list.categoryId.value = 1

    await vi.waitFor(() => expect(reads.at(-1)?.categoryId).toBe('1'))
    expect(list.page.value).toBe(1)
    expect(list.products.value.map(p => p.id)).toEqual([1, 2])
    expect(list.pageCount.value).toBe(2)

    list.clearFilters()

    await vi.waitFor(() => expect(list.products.value.map(p => p.id)).toEqual([1, 2]))
    expect(reads.at(-1)?.categoryId).toBeUndefined()
    expect(list.isFiltered.value).toBe(false)
  })

  it('moves back a page when a delete empties the last one', async () => {
    const list = await mountList()

    list.page.value = 3
    await vi.waitFor(() => expect(list.products.value.map(p => p.id)).toEqual([5]))

    // The only product on page 3 goes, the way a delete from the Products tab does it.
    stored = stored.filter(p => p.id !== 5)
    await list.refresh()

    // Left alone, the pager would point at an empty page 3 of 2.
    await vi.waitFor(() => expect(list.page.value).toBe(2))
    await vi.waitFor(() => expect(list.products.value.map(p => p.id)).toEqual([3, 4]))
  })

  it('re-reads the same rows when the reading language changes', async () => {
    const list = await mountList()

    expect(reads.at(-1)?.language).toBe('en')

    useLanguage().value = 'ar'

    await vi.waitFor(() => expect(reads.at(-1)?.language).toBe('ar'))
    expect(list.page.value).toBe(1)
  })
})
