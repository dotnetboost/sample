<script setup lang="ts">
import type { DropdownMenuItem } from '@nuxt/ui'
import type { Category, Product } from '~/types'

const { t } = useI18n()

useHead({ title: () => t('nav.documentTitle', { page: t('nav.catalog') }) })

const api = useApi()
const { notifySuccess, notifyFailure } = useNotify()
const language = useLanguage()

const tab = ref('categories')

const { data: languages } = await useAsyncData('catalog-languages', () => api.multiLingual.languages())
const { data: categories, refresh: refreshCategories } = await useAsyncData('catalog-categories', () => api.categories.list())

/**
 * Which page of products is on screen.
 *
 * How MANY is not here: `api.products.list()` is called with no page size, so the API answers with
 * `CatalogSettings.PageSize` and tells us what it used in `take`. Deciding it here as well would
 * be a second answer to a question the settings group already answers, and the two would disagree
 * the moment someone edited the setting.
 */
const page = ref(1)

const { data: productPage, refresh: refreshProducts } = await useAsyncData(
  'catalog-products',
  () => api.products.list({ page: page.value }),
  { watch: [page] })

// `currencySymbol` for the price, and `showProductsWithoutImages` so a filtered list can say so.
const { data: catalogSettings } = await useCatalogSettings()

const products = computed(() => productPage.value?.items ?? [])
const productTotal = computed(() => productPage.value?.total ?? 0)
const pageSize = computed(() => productPage.value?.take ?? 0)
const pageCount = computed(() =>
  pageSize.value ? Math.ceil(productTotal.value / pageSize.value) : 1)

const currencySymbol = computed(() => catalogSettings.value?.currencySymbol ?? '')
const imagesOnly = computed(() => catalogSettings.value?.showProductsWithoutImages === false)

const range = computed(() => productTotal.value
  ? t('catalog.products.range', {
      from: (productPage.value?.skip ?? 0) + 1,
      to: (productPage.value?.skip ?? 0) + products.value.length,
      total: productTotal.value
    })
  : '')

// Re-read when the header language changes: these are the same rows, translated.
watch(language, async () => {
  await Promise.all([refreshCategories(), refreshProducts()])
})

// A delete can empty the last page, which would otherwise leave a pager pointing past the end at
// an empty list. Mirrors what useMediaLibrary does for the same reason.
watch([productTotal, pageSize], () => {
  if (page.value > pageCount.value) {
    page.value = pageCount.value
  }
})

const editingCategory = ref<number | null>(null)
const categoryOpen = ref(false)
const editingProduct = ref<number | null>(null)
const productOpen = ref(false)

const confirming = ref<{ kind: 'category' | 'product', id: number, name: string } | null>(null)
const deleting = ref(false)

function editCategory(id: number | null) {
  editingCategory.value = id
  categoryOpen.value = true
}

function editProduct(id: number | null) {
  editingProduct.value = id
  productOpen.value = true
}

/**
 * A product was written. `createdId` is set only for a CREATE, and the editor stays open for one:
 * a product cannot hold media until it has a key, so its first image can only be attached from
 * that panel. Switching `editingProduct` onto the new id turns the open "new product" panel into
 * an "edit product" one, and the media sections that were showing "save first" become usable.
 *
 * Without this the create was a dead end whenever `showProductsWithoutImages` is off: the product
 * saved, dropped out of this list for having no images, and the catalogue is the only route back
 * into the editor — so there was no way to attach the image that would have made it visible.
 */
async function onProductSaved(createdId?: number) {
  if (createdId !== undefined) editingProduct.value = createdId

  await Promise.all([refreshProducts(), refreshCategories()])
}

async function confirmDelete() {
  if (!confirming.value) return

  deleting.value = true
  try {
    let message: string

    if (confirming.value.kind === 'category') {
      ({ message } = await api.categories.remove(confirming.value.id))
      await refreshCategories()
    } else {
      ({ message } = await api.products.remove(confirming.value.id))
      await Promise.all([refreshProducts(), refreshCategories()])
    }

    // "Deleted" on its own left the reader to work out which of two rows went. The API says which,
    // by slug or SKU, in the language the list is being read in — the same place the refusal in the
    // catch block comes from.
    notifySuccess(t('catalog.toast.deleted'), message)
    confirming.value = null
  } catch (error) {
    // The refusal that matters here is "this category still has products", which the API sends as
    // problem details with a localized `detail`. Reading `data.message` was left over from the
    // ad-hoc shape that endpoint used to answer with, and matched nothing any more: every delete
    // that was refused fell through to `String(error)` and showed a raw FetchError.
    notifyFailure(t('catalog.toast.deleteFailed'), error)
  } finally {
    deleting.value = false
  }
}

const categoryActions = (category: Category): DropdownMenuItem[][] => [[{
  label: t('common.edit'),
  icon: 'i-lucide-pencil',
  onSelect: () => editCategory(category.id)
}], [{
  label: t('common.delete'),
  icon: 'i-lucide-trash-2',
  color: 'error',
  disabled: category.products > 0,
  onSelect: () => confirming.value = { kind: 'category', id: category.id, name: category.name }
}]]

const productActions = (product: Product): DropdownMenuItem[][] => [[{
  label: t('common.edit'),
  icon: 'i-lucide-pencil',
  onSelect: () => editProduct(product.id)
}], [{
  label: t('common.delete'),
  icon: 'i-lucide-trash-2',
  color: 'error',
  onSelect: () => confirming.value = { kind: 'product', id: product.id, name: product.name }
}]]

const categoryName = (id: number) => categories.value?.find(c => c.id === id)?.name ?? '—'

// Computed so the tab labels follow the language, as the sidebar's do.
const items = computed(() => [
  { label: t('catalog.tabs.categories'), value: 'categories', icon: 'i-lucide-folder-tree' },
  { label: t('catalog.tabs.products'), value: 'products', icon: 'i-lucide-package' }
])
</script>

<template>
  <UDashboardPanel id="catalog">
    <template #header>
      <UDashboardNavbar :title="$t('nav.catalog')">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>

        <template #right>
          <LanguageSelect />
        </template>
      </UDashboardNavbar>

      <UDashboardToolbar>
        <template #left>
          <UTabs
            v-model="tab"
            :items="items"
            :content="false"
            size="sm"
          />
        </template>

        <template #right>
          <UButton
            v-if="tab === 'categories'"
            icon="i-lucide-plus"
            size="sm"
            :label="$t('catalog.newCategory')"
            @click="editCategory(null)"
          />
          <UButton
            v-else
            icon="i-lucide-plus"
            size="sm"
            :label="$t('catalog.newProduct')"
            @click="editProduct(null)"
          />
        </template>
      </UDashboardToolbar>
    </template>

    <template #body>
      <UPageCard
        :title="tab === 'categories' ? $t('catalog.categories.title') : $t('catalog.products.title')"
        :description="tab === 'categories'
          ? $t('catalog.categories.description')
          : $t('catalog.products.description')"
        variant="naked"
      />

      <!-- ── Categories ───────────────────────────────────────────────── -->
      <div v-if="tab === 'categories'" class="space-y-3">
        <UCard
          v-for="category in categories"
          :key="category.id"
          class="shrink-0"
          :ui="{ body: 'flex items-center gap-4' }"
        >
          <div
            class="flex size-14 shrink-0 items-center justify-center overflow-hidden rounded-lg border border-default bg-elevated/40"
          >
            <img
              v-if="category.banner"
              :src="contentUrl(category.banner.id)"
              :alt="category.banner.altText ?? category.name"
              class="size-full object-cover"
            >
            <UIcon v-else name="i-lucide-image-off" class="size-5 text-dimmed" />
          </div>

          <div class="min-w-0 flex-1">
            <p class="truncate font-medium">
              {{ category.name }}
            </p>
            <p class="truncate text-sm text-muted">
              {{ category.description }}
            </p>
          </div>

          <!-- `dir="ltr"`: a slug is a URL segment, declared [NotMultiLingual] for that reason. -->
          <UBadge
            variant="subtle"
            color="neutral"
            size="sm"
            dir="ltr"
          >
            {{ category.slug }}
          </UBadge>
          <UBadge variant="subtle" size="sm">
            {{ $t('catalog.productCount', category.products) }}
          </UBadge>

          <UDropdownMenu :items="categoryActions(category)">
            <UButton icon="i-lucide-ellipsis-vertical" color="neutral" variant="ghost" />
          </UDropdownMenu>
        </UCard>

        <UCard v-if="!categories?.length">
          <p class="text-muted">
            {{ $t('catalog.categories.empty') }}
          </p>
        </UCard>
      </div>

      <!-- ── Products ─────────────────────────────────────────────────── -->
      <div v-else class="space-y-3">
        <UCard
          v-for="product in products"
          :key="product.id"
          class="shrink-0"
          :ui="{ body: 'flex items-center gap-4' }"
        >
          <div
            class="flex size-14 shrink-0 items-center justify-center overflow-hidden rounded-lg border border-default bg-elevated/40"
          >
            <img
              v-if="product.primary"
              :src="contentUrl(product.primary.id)"
              :alt="product.primary.altText ?? product.name"
              class="size-full object-cover"
            >
            <UIcon v-else name="i-lucide-image-off" class="size-5 text-dimmed" />
          </div>

          <div class="min-w-0 flex-1">
            <p class="truncate font-medium">
              {{ product.name }}
            </p>
            <p class="truncate text-sm text-muted">
              {{ product.description }}
            </p>
          </div>

          <UBadge
            variant="subtle"
            color="neutral"
            size="sm"
            dir="ltr"
          >
            {{ product.sku }}
          </UBadge>
          <!-- No `dir` override, unlike the SKU beside it: `formatPrice` asks Intl to place the
               symbol for the reading locale and Intl marks the result's direction itself, so
               Arabic gets "349.00 $" and forcing LTR here would undo that. A SKU is a Latin code
               in every language and does need the override. -->
          <span class="text-sm">{{ formatPrice(product.price, currencySymbol) }}</span>
          <UBadge variant="subtle" size="sm">
            {{ categoryName(product.categoryId) }}
          </UBadge>
          <UBadge
            v-if="product.images.length"
            variant="subtle"
            color="neutral"
            size="sm"
          >
            {{ $t('catalog.imageCount', product.images.length) }}
          </UBadge>

          <UDropdownMenu :items="productActions(product)">
            <UButton icon="i-lucide-ellipsis-vertical" color="neutral" variant="ghost" />
          </UDropdownMenu>
        </UCard>

        <UCard v-if="!products.length">
          <p class="text-muted">
            {{ imagesOnly ? $t('catalog.products.emptyImagesOnly') : $t('catalog.products.empty') }}
          </p>
        </UCard>

        <div v-if="productTotal" class="flex items-center justify-between gap-3 pt-1">
          <p class="text-sm text-muted">
            {{ range }}
            <!-- Said rather than left to be noticed: a short list under this filter looks exactly
                 like a short catalogue. -->
            <span v-if="imagesOnly">· {{ $t('catalog.products.imagesOnly') }}</span>
          </p>

          <UPagination
            v-if="pageCount > 1"
            v-model:page="page"
            :total="productTotal"
            :items-per-page="pageSize"
          />
        </div>
      </div>

      <CategoryEditor
        v-model:open="categoryOpen"
        :category-id="editingCategory"
        :languages="languages ?? []"
        @saved="refreshCategories"
      />

      <ProductEditor
        v-model:open="productOpen"
        :product-id="editingProduct"
        :languages="languages ?? []"
        :categories="categories ?? []"
        @saved="onProductSaved"
      />

      <ConfirmModal
        :open="confirming !== null"
        :title="$t('catalog.confirmDelete.title', { name: confirming?.name ?? '' })"
        :description="$t('catalog.confirmDelete.description')"
        :confirm-label="$t('common.delete')"
        :loading="deleting"
        @update:open="confirming = null"
        @confirm="confirmDelete"
      />
    </template>
  </UDashboardPanel>
</template>
