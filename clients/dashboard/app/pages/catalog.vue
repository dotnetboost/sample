<script setup lang="ts">
import type { DropdownMenuItem } from '@nuxt/ui'
import type { Category, Product } from '~/types'

const { t } = useI18n()

useHead({ title: () => t('nav.documentTitle', { page: t('nav.catalog') }) })

const api = useApi()
const toast = useToast()
const language = useLanguage()

const tab = ref('categories')

const { data: languages } = await useAsyncData('catalog-languages', () => api.multiLingual.languages())
const { data: categories, refresh: refreshCategories } = await useAsyncData('catalog-categories', () => api.categories.list())
const { data: products, refresh: refreshProducts } = await useAsyncData('catalog-products', () => api.products.list())

// Re-read when the header language changes: these are the same rows, translated.
watch(language, async () => {
  await Promise.all([refreshCategories(), refreshProducts()])
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

async function confirmDelete() {
  if (!confirming.value) return

  deleting.value = true
  try {
    if (confirming.value.kind === 'category') {
      await api.categories.remove(confirming.value.id)
      await refreshCategories()
    } else {
      await api.products.remove(confirming.value.id)
      await Promise.all([refreshProducts(), refreshCategories()])
    }
    toast.add({ title: t('catalog.toast.deleted'), color: 'success' })
    confirming.value = null
  } catch (error: unknown) {
    const message = (error as { data?: { message?: string } })?.data?.message ?? String(error)
    toast.add({ title: t('catalog.toast.deleteFailed'), description: message, color: 'error' })
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
          <span class="text-sm">{{ product.price }}</span>
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

        <UCard v-if="!products?.length">
          <p class="text-muted">
            {{ $t('catalog.products.empty') }}
          </p>
        </UCard>
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
        @saved="() => { refreshProducts(); refreshCategories() }"
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
