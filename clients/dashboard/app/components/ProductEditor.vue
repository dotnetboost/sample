<script setup lang="ts">
import { Info } from '@lucide/vue'
import { toast } from 'vue-sonner'
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

async function save() {
  if (categoryId.value === undefined) {
    // Not an API failure — nothing has been sent yet — so this one is raised directly.
    toast.error(t('productEditor.toast.pickCategory'))
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
  <EditorSheet
    v-model:open="open"
    :title="productId === null ? $t('productEditor.titleNew') : $t('productEditor.titleEdit')"
    :description="$t('productEditor.description')"
  >
    <div v-if="loading" class="flex items-center justify-center gap-2 py-10 text-muted-foreground">
      <Spinner />
      {{ $t('common.loading') }}
    </div>

    <FieldGroup v-else>
      <div class="grid gap-4 sm:grid-cols-2">
        <Field>
          <FieldLabel for="product-sku">
            {{ $t('productEditor.sku.label') }}
          </FieldLabel>
          <Input
            id="product-sku"
            v-model="sku"
            dir="ltr"
            :placeholder="$t('productEditor.sku.placeholder')"
          />
          <FieldDescription>{{ $t('productEditor.sku.help') }}</FieldDescription>
        </Field>

        <Field>
          <FieldLabel for="product-price">
            {{ $t('productEditor.price') }}
          </FieldLabel>
          <Input
            id="product-price"
            v-model.number="price"
            type="number"
            step="0.01"
          />
        </Field>
      </div>

      <Field>
        <FieldLabel for="product-category">
          {{ $t('productEditor.category') }}
        </FieldLabel>
        <Select v-model="categoryId">
          <SelectTrigger id="product-category" class="w-full">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem v-for="category in categories" :key="category.id" :value="category.id">
              {{ category.name }}
            </SelectItem>
          </SelectContent>
        </Select>
      </Field>

      <Field>
        <FieldTitle>{{ $t('productEditor.textPerLanguage') }}</FieldTitle>
        <TranslationEditor
          v-model="translations"
          :languages="languages"
          :fields="[
            { name: 'Name', label: $t('translationFields.name') },
            { name: 'Description', label: $t('translationFields.description'), textarea: true }
          ]"
        />
      </Field>

      <Field v-if="productId !== null">
        <FieldTitle>{{ $t('productEditor.media') }}</FieldTitle>
        <EntityAttachments
          entity-name="Product"
          :entity-id="String(productId)"
          :images="images"
          @changed="refreshImages"
        />
      </Field>

      <Alert v-else>
        <Info />
        <AlertTitle>{{ $t('productEditor.saveFirst.title') }}</AlertTitle>
        <AlertDescription>{{ $t('productEditor.saveFirst.description') }}</AlertDescription>
      </Alert>
    </FieldGroup>

    <template #footer>
      <Button variant="ghost" @click="open = false">
        {{ $t('common.cancel') }}
      </Button>
      <Button :disabled="saving" @click="save">
        <Spinner v-if="saving" />
        {{ $t('common.save') }}
      </Button>
    </template>
  </EditorSheet>
</template>
