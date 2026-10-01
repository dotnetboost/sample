<script setup lang="ts">
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
const toast = useToast()

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
    if (props.categoryId === null) {
      await api.categories.create(body)
    } else {
      await api.categories.update(props.categoryId, body)
    }

    toast.add({ title: props.categoryId === null ? 'Category created' : 'Category saved', color: 'success' })
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
    :title="categoryId === null ? 'New category' : 'Edit category'"
    description="Slug is a column. Text is translation rows. The banner is a media attachment."
    :ui="{ content: 'max-w-xl' }"
  >
    <template #body>
      <div v-if="loading" class="py-10 text-center text-muted">
        Loading…
      </div>

      <div v-else class="space-y-6">
        <UFormField
          label="Slug"
          help="Not translated — a URL segment should be the same string in every language, which is what [NotMultiLingual] declares."
        >
          <UInput v-model="slug" placeholder="furniture" class="w-full" />
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

        <div v-if="categoryId !== null">
          <p class="mb-2 text-sm font-medium">
            Media
          </p>
          <EntityAttachments
            entity-name="Category"
            :entity-id="String(categoryId)"
            :images="images"
            @changed="refreshImages"
          />
        </div>

        <UAlert
          v-else
          icon="i-lucide-info"
          variant="subtle"
          color="neutral"
          title="Save first to attach media"
          description="An attachment needs the category's key, which it does not have until it is saved."
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
