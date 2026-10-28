<script setup lang="ts">
import { Info } from '@lucide/vue'
import type { EntityImage, Language, Translations } from '~/types'

/**
 * Editing one category: its slug, its text in every language, and its banner.
 *
 * Three libraries' data on one form, and the form does not know that. The slug is a column on the
 * Categories table; the text is rows in DotNetBoost.MultiLingual; the banner is an attachment row
 * in DotNetBoost.Media. Each is saved by its own call.
 */
const props = defineProps<{
  categoryId: number | null
  languages: Language[]
}>()

const open = defineModel<boolean>('open', { default: false })
const emit = defineEmits<{ saved: [] }>()

const api = useApi()
const { notifySuccess, notifyFailure } = useNotify()
const { t } = useI18n()

const slug = ref('')
const translations = ref<Translations>({})
const images = ref<EntityImage[]>([])
const saving = ref(false)
const loading = ref(false)

watch(() => [open.value, props.categoryId] as const, async ([isOpen, id]) => {
  if (!isOpen) return

  if (id === null) {
    slug.value = ''
    translations.value = Object.fromEntries(props.languages.map(l => [l.code, {}]))
    images.value = []
    return
  }

  await reload(id)
}, { immediate: true })

async function reload(id: number) {
  loading.value = true
  try {
    const detail = await api.categories.get(id)
    slug.value = detail.slug
    // Start from every language, so one with nothing stored still gets a button.
    translations.value = Object.fromEntries(
      props.languages.map(l => [l.code, { ...(detail.translations[l.code] ?? {}) }]))
    images.value = detail.images
  } finally {
    loading.value = false
  }
}

/** The attachments landed in the media library already; just re-read what is there. */
async function refreshImages() {
  if (props.categoryId !== null) await reload(props.categoryId)
  emit('saved')
}

async function save() {
  saving.value = true
  try {
    const body = { slug: slug.value, translations: translations.value }

    // Media is NOT saved here: an attach or a detach landed as its own request when it was made,
    // against the media library's own endpoints. Saving a category cannot touch its files, because
    // they are attachment rows pointing at it rather than columns on it.
    const written = props.categoryId === null
      ? await api.categories.create(body)
      : await api.categories.update(props.categoryId, body)

    // The title is this dashboard's — short, and the same every time, which is what makes a toast
    // scannable. The description is the API's: it names the slug the write landed on, and it is
    // already in the reading language, so there is nothing here to translate.
    notifySuccess(
      props.categoryId === null
        ? t('categoryEditor.toast.created')
        : t('categoryEditor.toast.saved'),
      written.message)

    emit('saved')
    open.value = false
  } catch (error) {
    notifyFailure(t('categoryEditor.toast.failed'), error)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <EditorSheet
    v-model:open="open"
    :title="categoryId === null ? $t('categoryEditor.titleNew') : $t('categoryEditor.titleEdit')"
    :description="$t('categoryEditor.description')"
  >
    <div v-if="loading" class="flex items-center justify-center gap-2 py-10 text-muted-foreground">
      <Spinner />
      {{ $t('common.loading') }}
    </div>

    <FieldGroup v-else>
      <Field>
        <FieldLabel for="category-slug">
          {{ $t('categoryEditor.slug.label') }}
        </FieldLabel>
        <Input
          id="category-slug"
          v-model="slug"
          dir="ltr"
          :placeholder="$t('categoryEditor.slug.placeholder')"
        />
        <FieldDescription>{{ $t('categoryEditor.slug.help') }}</FieldDescription>
      </Field>

      <Field>
        <FieldTitle>
          {{ $t('categoryEditor.textPerLanguage') }}
        </FieldTitle>
        <TranslationEditor
          v-model="translations"
          :languages="languages"
          :fields="[
            { name: 'Name', label: $t('translationFields.name') },
            { name: 'Description', label: $t('translationFields.description'), textarea: true }
          ]"
        />
      </Field>

      <Field v-if="categoryId !== null">
        <FieldTitle>
          {{ $t('categoryEditor.media') }}
        </FieldTitle>
        <EntityAttachments
          entity-name="Category"
          :entity-id="String(categoryId)"
          :images="images"
          @changed="refreshImages"
        />
      </Field>

      <Alert v-else>
        <Info />
        <AlertTitle>{{ $t('categoryEditor.saveFirst.title') }}</AlertTitle>
        <AlertDescription>{{ $t('categoryEditor.saveFirst.description') }}</AlertDescription>
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
