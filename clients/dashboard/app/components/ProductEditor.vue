<script setup lang="ts">
import type { Category, EntityImage, Language, Translations } from '~/types'

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
const { t } = useI18n()

const sku = ref('')
const price = ref(0)
const categoryId = ref<number | undefined>()
const translations = ref<Translations>({})
const images = ref<EntityImage[]>([])
const saving = ref(false)
const loading = ref(false)

watch(() => [open.value, props.productId] as const, async ([isOpen, id]) => {
  if (!isOpen) return

  if (id === null) {
    sku.value = ''
    price.value = 0
    categoryId.value = props.categories[0]?.id
    translations.value = Object.fromEntries(props.languages.map(l => [l.code, {}]))
    images.value = []
    return
  }

  await reload(id)
}, { immediate: true })

async function reload(id: number) {
  loading.value = true
  try {
    const detail = await api.products.get(id)
    sku.value = detail.sku
    price.value = detail.price
    categoryId.value = detail.categoryId
    translations.value = Object.fromEntries(
      props.languages.map(l => [l.code, { ...(detail.translations[l.code] ?? {}) }]))
    images.value = detail.images
  } finally {
    loading.value = false
  }
}

/** The attachments landed in the media library already; just re-read what is there. */
async function refreshImages() {
  if (props.productId !== null) await reload(props.productId)
  emit('saved')
}

const categoryItems = computed(() =>
  props.categories.map(c => ({ label: c.name, value: c.id })))

async function save() {
  if (categoryId.value === undefined) {
    toast.add({ title: t('productEditor.toast.pickCategory'), color: 'error' })
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

    // Media is NOT saved here: an attach, detach or reorder landed as its own request when it was
    // made, against the media library's own endpoints. A product's files are attachment rows
    // pointing at it, not columns on it.
    if (props.productId === null) {
      await api.products.create(body)
    } else {
      await api.products.update(props.productId, body)
    }

    toast.add({
      title: props.productId === null
        ? t('productEditor.toast.created')
        : t('productEditor.toast.saved'),
      color: 'success'
    })
    emit('saved')
    open.value = false
  } catch (error) {
    toast.add({
      title: t('productEditor.toast.failed'),
      description: problemMessage(error),
      color: 'error'
    })
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <USlideover
    v-model:open="open"
    :title="productId === null ? $t('productEditor.titleNew') : $t('productEditor.titleEdit')"
    :description="$t('productEditor.description')"
    :ui="{ content: 'max-w-xl' }"
  >
    <template #body>
      <div v-if="loading" class="py-10 text-center text-muted">
        {{ $t('common.loading') }}
      </div>

      <div v-else class="space-y-6">
        <div class="grid gap-3 sm:grid-cols-2">
          <UFormField :label="$t('productEditor.sku.label')" :help="$t('productEditor.sku.help')">
            <UInput
              v-model="sku"
              dir="ltr"
              :placeholder="$t('productEditor.sku.placeholder')"
              class="w-full"
            />
          </UFormField>

          <UFormField :label="$t('productEditor.price')">
            <UInput
              v-model.number="price"
              type="number"
              step="0.01"
              class="w-full"
            />
          </UFormField>
        </div>

        <UFormField :label="$t('productEditor.category')">
          <USelect
            v-model="categoryId"
            :items="categoryItems"
            value-key="value"
            class="w-full"
          />
        </UFormField>

        <div>
          <p class="mb-2 text-sm font-medium">
            {{ $t('productEditor.textPerLanguage') }}
          </p>
          <TranslationEditor
            v-model="translations"
            :languages="languages"
            :fields="[
              { name: 'Name', label: $t('translationFields.name') },
              { name: 'Description', label: $t('translationFields.description'), textarea: true }
            ]"
          />
        </div>

        <div v-if="productId !== null">
          <p class="mb-2 text-sm font-medium">
            {{ $t('productEditor.media') }}
          </p>
          <EntityAttachments
            entity-name="Product"
            :entity-id="String(productId)"
            :images="images"
            @changed="refreshImages"
          />
        </div>

        <UAlert
          v-else
          icon="i-lucide-info"
          variant="subtle"
          color="neutral"
          :title="$t('productEditor.saveFirst.title')"
          :description="$t('productEditor.saveFirst.description')"
        />
      </div>
    </template>

    <template #footer>
      <div class="flex w-full justify-end gap-2">
        <UButton
          :label="$t('common.cancel')"
          color="neutral"
          variant="ghost"
          @click="open = false"
        />
        <UButton :loading="saving" :label="$t('common.save')" @click="save" />
      </div>
    </template>
  </USlideover>
</template>
