import { watchDebounced } from '@vueuse/core'
import type { Product } from '~/types'

export const PRODUCT_LIST_KEY = 'catalog-products'

// One empty array for "nothing yet", not a fresh `[]` per read: the products table takes this as
// its data, and a new array each time is a new dataset each time.
const noProducts: Product[] = []

/**
 * One page of the products listing, and the question it answers: a search, a category, a page.
 *
 * **How many per page is not here.** `api.products.list()` is called with no page size, so the API
 * answers with `CatalogSettings.PageSize` and says what it used in `take`. Deciding it here as well
 * would be a second answer to a question the settings group already answers.
 *
 * **Both filters are parameters on `GET /api/products`**, never a filter over the rows on screen,
 * which would only ever search the page you are looking at. The search is matched in the reading
 * language (see `ListProducts`), so the Arabic name finds a product while the dashboard is in
 * Arabic. `searchBox` is what the input binds, debounced into `search`, so a keystroke is not a
 * request. Either one changing goes back to page 1: page 3 of everything can be past the end of
 * one category.
 *
 * Awaited by the caller, so the first render already has a page. Everything reactive is created
 * before the await, which is what keeps it tied to the calling component's lifetime.
 */
export async function useProductList() {
  const api = useApi()
  const { t } = useI18n()
  const language = useLanguage()

  const page = ref(1)
  const searchBox = ref('')
  const search = ref('')
  const categoryId = ref<number | undefined>()

  watchDebounced(searchBox, (value) => {
    search.value = value.trim()
  }, { debounce: 300 })

  watch([search, categoryId], () => {
    page.value = 1
  })

  const isFiltered = computed(() => !!search.value || categoryId.value !== undefined)

  function clearFilters() {
    searchBox.value = ''
    search.value = ''
    categoryId.value = undefined
  }

  // The language is watched too: the same rows, re-read in the new language.
  const asyncData = useAsyncData(
    PRODUCT_LIST_KEY,
    (_nuxtApp, { signal }) => api.products.list({
      page: page.value,
      search: search.value,
      categoryId: categoryId.value
    }, { signal }),
    { watch: [page, search, categoryId, language] })

  const { data, pending, refresh } = asyncData

  const products = computed(() => data.value?.items ?? noProducts)
  const total = computed(() => data.value?.total ?? 0)
  const pageSize = computed(() => data.value?.take ?? 0)

  // Never below 1: a filter that matches nothing is one empty page, not none, and a count of zero
  // would have the clamp below send the pager to page 0.
  const pageCount = computed(() =>
    pageSize.value ? Math.max(1, Math.ceil(total.value / pageSize.value)) : 1)

  /** "1–12 of 30", or nothing for an empty listing. */
  const range = computed(() => {
    if (!total.value) return ''

    const skip = data.value?.skip ?? 0

    return t('catalog.products.range', { from: skip + 1, to: skip + products.value.length, total: total.value })
  })

  // A delete can empty the last page, which would leave the pager pointing past the end.
  watch([total, pageSize], () => {
    if (page.value > pageCount.value) {
      page.value = pageCount.value
    }
  })

  await asyncData

  return {
    page, searchBox, categoryId, isFiltered, clearFilters,
    products, total, pageSize, pageCount, range, pending, refresh
  }
}
