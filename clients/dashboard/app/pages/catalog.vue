<script setup lang="ts">
import type { DropdownMenuItem } from '@nuxt/ui'
import type { Category, Product } from '~/types'

useHead({ title: 'Catalog — DotNetBoost Sample' })

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
    toast.add({ title: 'Deleted', color: 'success' })
    confirming.value = null
  } catch (error: unknown) {
    const message = (error as { data?: { message?: string } })?.data?.message ?? String(error)
    toast.add({ title: 'Could not delete', description: message, color: 'error' })
  } finally {
    deleting.value = false
  }
}

const categoryActions = (category: Category): DropdownMenuItem[][] => [[{
  label: 'Edit',
  icon: 'i-lucide-pencil',
  onSelect: () => editCategory(category.id)
}], [{
  label: 'Delete',
  icon: 'i-lucide-trash-2',
  color: 'error',
  disabled: category.products > 0,
  onSelect: () => confirming.value = { kind: 'category', id: category.id, name: category.name }
}]]

const productActions = (product: Product): DropdownMenuItem[][] => [[{
  label: 'Edit',
  icon: 'i-lucide-pencil',
  onSelect: () => editProduct(product.id)
}], [{
  label: 'Delete',
  icon: 'i-lucide-trash-2',
  color: 'error',
  onSelect: () => confirming.value = { kind: 'product', id: product.id, name: product.name }
}]]

const categoryName = (id: number) => categories.value?.find(c => c.id === id)?.name ?? '—'

const items = [
  { label: 'Categories', value: 'categories', icon: 'i-lucide-folder-tree' },
  { label: 'Products', value: 'products', icon: 'i-lucide-package' }
]
</script>

<template>
  <UDashboardPanel id="catalog">
    <template #header>
      <UDashboardNavbar title="Catalog">
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
            label="New category"
            @click="editCategory(null)"
          />
          <UButton
            v-else
            icon="i-lucide-plus"
            size="sm"
            label="New product"
            @click="editProduct(null)"
          />
        </template>
      </UDashboardToolbar>
    </template>

    <template #body>
      <UPageCard
        :title="tab === 'categories' ? 'Categories' : 'Products'"
        :description="tab === 'categories'
          ? 'The application\'s own entity. Name and description are not columns on this table — they are translation rows, returned already filled in for the language in the header.'
          : 'Name and description are translation rows; images are attachments in the media library. Neither library knows the other exists.'"
        variant="naked"
      />

      <!-- ── Categories ───────────────────────────────────────────────── -->
      <div v-if="tab === 'categories'" class="space-y-3">
        <UCard
          v-for="category in categories"
          :key="category.id"
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

          <UBadge variant="subtle" color="neutral" size="sm">
            {{ category.slug }}
          </UBadge>
          <UBadge variant="subtle" size="sm">
            {{ category.products }} products
          </UBadge>

          <UDropdownMenu :items="categoryActions(category)">
            <UButton icon="i-lucide-ellipsis-vertical" color="neutral" variant="ghost" />
          </UDropdownMenu>
        </UCard>

        <UCard v-if="!categories?.length">
          <p class="text-muted">
            No categories yet.
          </p>
        </UCard>
      </div>

      <!-- ── Products ─────────────────────────────────────────────────── -->
      <div v-else class="space-y-3">
        <UCard
          v-for="product in products"
          :key="product.id"
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

          <UBadge variant="subtle" color="neutral" size="sm">
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
            {{ product.images.length }} images
          </UBadge>

          <UDropdownMenu :items="productActions(product)">
            <UButton icon="i-lucide-ellipsis-vertical" color="neutral" variant="ghost" />
          </UDropdownMenu>
        </UCard>

        <UCard v-if="!products?.length">
          <p class="text-muted">
            No products yet.
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
        :title="`Delete ${confirming?.name ?? ''}?`"
        description="This cannot be undone. A product's images and their alt text go with it — written out by hand, because nothing cascades across library boundaries."
        confirm-label="Delete"
        :loading="deleting"
        @update:open="confirming = null"
        @confirm="confirmDelete"
      />
    </template>
  </UDashboardPanel>
</template>
