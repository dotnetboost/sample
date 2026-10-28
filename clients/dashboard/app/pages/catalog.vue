<script setup lang="ts">
import { FolderTree, Package } from '@lucide/vue'
import type { Category } from '~/types'

/**
 * The catalogue: a tab each for categories and products. Each tab owns its table, its editor and
 * its deletes; this page owns what both read — the categories, which the Products tab filters by
 * and names — and which tab is open.
 */
const { t } = useI18n()

useHead({ title: () => t('nav.documentTitle', { page: t('nav.catalog') }) })

const api = useApi()
const language = useLanguage()

/**
 * Which tab is open, kept in the address as `?tab=products` — so the sidebar can link straight to
 * either tab, and a reload or a shared link lands on the one that was open. Categories is the
 * default and takes no parameter, so `/catalog` stays the plain address of the page.
 *
 * `replace` rather than `push`: switching tabs is looking at the other half of one page, and a
 * back button that walked through every tab switch would be a back button that does not leave.
 */
const route = useRoute()
const router = useRouter()

const tab = computed<'categories' | 'products'>({
  get: () => route.query.tab === 'products' ? 'products' : 'categories',
  set: value => router.replace({ query: { ...route.query, tab: value === 'products' ? 'products' : undefined } })
})

// The one shared language list: a language added on the Languages page has to reach the
// translation editors without a reload.
const { languages } = useLanguageList()

// Re-read when the header language changes: the same rows, translated.
const { data: categories, refresh: refreshCategories } = await useAsyncData(
  'catalog-categories',
  (_nuxtApp, { signal }) => api.categories.list({ signal }),
  { watch: [language] })

// One empty array for "nothing yet", not a fresh `[]` per read: the categories table takes this
// as its data, and a new array each time is a new dataset each time.
const noCategories: Category[] = []
const categoryRows = computed(() => categories.value ?? noCategories)
</script>

<template>
  <DashboardPage :title="$t('nav.catalog')">
    <template #toolbar>
      <Tabs v-model="tab">
        <TabsList>
          <TabsTrigger value="categories">
            <FolderTree />
            {{ $t('catalog.tabs.categories') }}
          </TabsTrigger>
          <TabsTrigger value="products">
            <Package />
            {{ $t('catalog.tabs.products') }}
          </TabsTrigger>
        </TabsList>
      </Tabs>
    </template>

    <SectionIntro
      :title="tab === 'categories' ? $t('catalog.categories.title') : $t('catalog.products.title')"
      :description="tab === 'categories'
        ? $t('catalog.categories.description')
        : $t('catalog.products.description')"
    />

    <CatalogCategoriesTab
      v-if="tab === 'categories'"
      :categories="categoryRows"
      :languages="languages ?? []"
      @changed="refreshCategories"
    />

    <CatalogProductsTab
      v-else
      :categories="categoryRows"
      :languages="languages ?? []"
      @changed="refreshCategories"
    />
  </DashboardPage>
</template>
