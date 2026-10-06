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
/**
 * `saved` carries the new row's id on a CREATE, and nothing on an update. The parent switches the
 * open editor onto that id, which is what keeps a just-created product reachable — see `save()`.
 */
const emit = defineEmits<{ saved: [createdId?: number] }>()

const api = useApi()
const toast = useToast()
const { notifySuccess, notifyFailure } = useNotify()
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
    // Not an API failure — nothing has been sent yet — so this one is raised directly.
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
      const created = await api.products.create(body)

      // Title from here, sentence from the API — see CategoryEditor.
      notifySuccess(t('productEditor.toast.created'), created.message)

      // Deliberately STAYS OPEN, on the product just created. A product cannot hold media until
      // it has a key, so this panel is the only place its first image can be attached — and
      // closing here would send you back to the list to find the new row and reopen it before you
      // could. The parent swaps `productId` to the new id, the watcher above reloads, and the
      // media sections that were showing "save first" become usable.
      emit('saved', created.id)
      return
    }

    const written = await api.products.update(props.productId, body)

    notifySuccess(t('productEditor.toast.saved'), written.message)

    emit('saved')
    open.value = false
  } catch (error) {
    notifyFailure(t('productEditor.toast.failed'), error)
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
