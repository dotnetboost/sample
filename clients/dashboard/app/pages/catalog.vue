<script setup lang="ts">
const api = useApi()
const toast = useToast()
const language = useLanguage()
const config = useRuntimeConfig()

const { data: categories, refresh: refreshCategories }
  = await useAsyncData('catalog-categories', () => api.catalog.categories())

const { data: products, refresh: refreshProducts }
  = await useAsyncData('catalog-products', () => api.catalog.products())

// Re-read everything when the header's language changes: these are the same rows, translated.
watch(language, async () => {
  await Promise.all([refreshCategories(), refreshProducts()])
})

const uploading = ref<number | null>(null)

async function upload(productId: number, event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  if (file.size > Number(config.public.maxUploadBytes)) {
    toast.add({ title: 'File too large', color: 'error' })
    input.value = ''
    return
  }

  uploading.value = productId
  try {
    await api.catalog.uploadImage(productId, file, 'gallery')
    await refreshProducts()
    toast.add({
      title: 'Uploaded',
      description: 'Add alt text per language on the Media page.',
      color: 'success'
    })
  } catch (error) {
    toast.add({ title: 'Upload failed', description: String(error), color: 'error' })
  } finally {
    uploading.value = null
    input.value = ''
  }
}

const categoryName = (id: number) =>
  categories.value?.find(c => c.id === id)?.name ?? '—'
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-semibold">
        Catalog
      </h1>
      <p class="mt-1 text-muted">
        Categories and products — the application's own two entities. Their names and descriptions
        are not columns on those tables: they are translation rows, and
        <code>db.Products.ToListAsync()</code> returns them already filled in for
        <strong>{{ language }}</strong>.
      </p>
    </div>

    <UCard>
      <template #header>
        <h2 class="font-semibold">
          Categories
        </h2>
      </template>

      <div class="divide-y divide-default">
        <div
          v-for="category in categories"
          :key="category.id"
          class="flex items-center gap-4 py-3 first:pt-0 last:pb-0"
        >
          <div class="flex-1">
            <p class="font-medium">
              {{ category.name }}
            </p>
            <p class="text-sm text-muted">
              {{ category.description }}
            </p>
          </div>
          <UBadge variant="subtle" color="neutral">
            {{ category.slug }}
          </UBadge>
          <UBadge variant="subtle">
            {{ category.products }} products
          </UBadge>
        </div>
      </div>

      <template #footer>
        <p class="text-xs text-muted">
          <code>slug</code> carries <code>[NotMultiLingual]</code> — a URL segment should be the
          same string in every language.
        </p>
      </template>
    </UCard>

    <UCard>
      <template #header>
        <h2 class="font-semibold">
          Products
        </h2>
      </template>

      <div class="space-y-5 divide-y divide-default">
        <div
          v-for="product in products"
          :key="product.id"
          class="pt-5 first:pt-0"
        >
          <div class="flex flex-wrap items-start gap-4">
            <div class="min-w-0 flex-1">
              <div class="flex items-center gap-2">
                <p class="font-medium">
                  {{ product.name }}
                </p>
                <UBadge variant="subtle" color="neutral" size="sm">
                  {{ product.sku }}
                </UBadge>
              </div>
              <p class="mt-0.5 text-sm text-muted">
                {{ product.description }}
              </p>
              <p class="mt-1 text-sm">
                {{ product.price }} · {{ categoryName(product.categoryId) }}
              </p>
            </div>

            <UButton
              as="label"
              icon="i-lucide-upload"
              variant="subtle"
              size="sm"
              :loading="uploading === product.id"
              label="Add image"
            >
              <input
                type="file"
                accept="image/*"
                class="hidden"
                @change="upload(product.id, $event)"
              >
            </UButton>
          </div>

          <div v-if="product.images.length" class="mt-3 flex flex-wrap gap-3">
            <div
              v-for="image in product.images"
              :key="image.id"
              class="w-44 space-y-1"
            >
              <img
                :src="image.url"
                :alt="image.altText ?? ''"
                class="h-28 w-44 rounded border border-default object-cover"
              >
              <p v-if="image.altText" class="text-xs text-muted">
                <UIcon name="i-lucide-check" class="text-success" />
                {{ image.altText }}
              </p>
              <p v-else class="text-xs text-warning">
                <UIcon name="i-lucide-triangle-alert" />
                no alt text in {{ language }}
              </p>
            </div>
          </div>

          <p v-else class="mt-3 text-sm text-muted">
            No images yet.
          </p>
        </div>
      </div>

      <template #footer>
        <p class="text-xs text-muted">
          Images come from DotNetBoost.Media, addressed by
          <code>("Product", id)</code>. The alt text next to each one comes from
          DotNetBoost.MultiLingual, addressed by <code>("MediaFile", fileId)</code>. The two were
          joined in <code>Integration/MediaText.cs</code>.
        </p>
      </template>
    </UCard>
  </div>
</template>
