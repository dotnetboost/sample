<script setup lang="ts">
/**
 * Editing one product: its own columns, its text in every language, its primary image, and its
 * gallery. Same three-library shape as CategoryEditor, with a gallery on top — a product declares
 * `One("primary").Many("gallery")`, so primary replaces and gallery appends.
 */
const props = defineProps<{
  productId: number | null
  languages: Language[]
  categories: Category[]
}>()

const open = defineModel<boolean>('open', { default: false })
const emit = defineEmits<{ saved: [] }>()

const api = useApi()
const toast = useToast()

const sku = ref('')
const price = ref(0)
const categoryId = ref<number | null>(null)
const translations = ref<Translations>({})
const primary = ref<MediaFile | null>(null)
const gallery = ref<MediaFile[]>([])
const pending = ref<MediaFile | null>(null)
const saving = ref(false)
const loading = ref(false)

watch(() => [open.value, props.productId] as const, async ([isOpen, id]) => {
  if (!isOpen) return

  pending.value = null

  if (id === null) {
    sku.value = ''
    price.value = 0
    categoryId.value = props.categories[0]?.id ?? null
    translations.value = Object.fromEntries(props.languages.map(l => [l.code, {}]))
    primary.value = null
    gallery.value = []
    return
  }

  loading.value = true
  try {
    const detail = await api.products.get(id)
    sku.value = detail.sku
    price.value = detail.price
    categoryId.value = detail.categoryId
    translations.value = Object.fromEntries(
      props.languages.map(l => [l.code, { ...(detail.translations[l.code] ?? {}) }]))
    primary.value = detail.primary
    gallery.value = detail.gallery
  } finally {
    loading.value = false
  }
}, { immediate: true })

const categoryItems = computed(() =>
  props.categories.map(c => ({ label: c.name, value: c.id })))

async function addToGallery() {
  if (!pending.value || props.productId === null) return

  await api.products.addToGallery(props.productId, pending.value.id)
  gallery.value = [...gallery.value, pending.value]
  pending.value = null
}

async function removeFromGallery(file: MediaFile) {
  if (props.productId === null) return

  await api.products.removeFromGallery(props.productId, file.id)
  gallery.value = gallery.value.filter(f => f.id !== file.id)
}

async function save() {
  if (categoryId.value === null) {
    toast.add({ title: 'Pick a category', color: 'error' })
    return
  }

  saving.value = true
  try {
    const body = {
      sku: sku.value,
      price: price.value,
      categoryId: categoryId.value,
      translations: translations.value
    }

    const id = props.productId
      ?? (await api.products.create(body) as { id: number }).id

    if (props.productId !== null) await api.products.update(id, body)

    await api.products.setPrimary(id, primary.value?.id ?? null)

    toast.add({ title: props.productId === null ? 'Product created' : 'Product saved', color: 'success' })
    emit('saved')
    open.value = false
  } catch (error) {
    toast.add({ title: 'Save failed', description: String(error), color: 'error' })
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <USlideover
    v-model:open="open"
    :title="productId === null ? 'New product' : 'Edit product'"
    description="SKU and price are columns. Name and description are translation rows. Images are media attachments."
    :ui="{ content: 'max-w-xl' }"
  >
    <template #body>
      <div v-if="loading" class="py-10 text-center text-muted">
        Loading…
      </div>

      <div v-else class="space-y-6">
        <div class="grid gap-3 sm:grid-cols-2">
          <UFormField label="SKU" help="Not translated.">
            <UInput v-model="sku" placeholder="DESK-001" class="w-full" />
          </UFormField>

          <UFormField label="Price">
            <UInput
              v-model.number="price"
              type="number"
              step="0.01"
              class="w-full"
            />
          </UFormField>
        </div>

        <UFormField label="Category">
          <USelect
            v-model="categoryId"
            :items="categoryItems"
            value-key="value"
            class="w-full"
          />
        </UFormField>

        <div>
          <p class="mb-2 text-sm font-medium">
            Text, per language
          </p>
          <TranslationEditor
            v-model="translations"
            :languages="languages"
            :fields="[
              { name: 'Name', label: 'Name' },
              { name: 'Description', label: 'Description', textarea: true }
            ]"
          />
        </div>

        <MediaPicker
          v-model="primary"
          label="Primary image"
          hint="Role 'primary' — exactly one per product, so picking replaces."
        />

        <div v-if="productId !== null" class="space-y-3">
          <p class="text-sm font-medium">
            Gallery
          </p>

          <div v-if="gallery.length" class="flex flex-wrap gap-2">
            <div
              v-for="file in gallery"
              :key="file.id"
              class="relative"
            >
              <img
                :src="mediaUrl(file)"
                :alt="file.altText ?? file.fileName"
                class="size-20 rounded border border-default object-cover"
              >
              <UButton
                icon="i-lucide-x"
                size="xs"
                color="error"
                variant="solid"
                class="absolute -end-1.5 -top-1.5 rounded-full"
                @click="removeFromGallery(file)"
              />
            </div>
          </div>

          <p v-else class="text-sm text-muted">
            No gallery images.
          </p>

          <div class="flex items-end gap-2">
            <div class="flex-1">
              <MediaPicker v-model="pending" label="Add to gallery" />
            </div>
            <UButton
              :disabled="!pending"
              size="sm"
              label="Add"
              @click="addToGallery"
            />
          </div>

          <p class="text-xs text-muted">
            Role 'gallery' — many per product, appended in order. Removing detaches the
            attachment; the file stays in the library because another entity may be using it.
          </p>
        </div>

        <UAlert
          v-else
          icon="i-lucide-info"
          variant="subtle"
          color="neutral"
          title="Save first to add gallery images"
          description="A gallery attachment needs the product's key, which it does not have until it is saved."
        />
      </div>
    </template>

    <template #footer>
      <div class="flex w-full justify-end gap-2">
        <UButton
          label="Cancel"
          color="neutral"
          variant="ghost"
          @click="open = false"
        />
        <UButton :loading="saving" label="Save" @click="save" />
      </div>
    </template>
  </USlideover>
</template>
