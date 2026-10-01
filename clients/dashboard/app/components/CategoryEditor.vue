<script setup lang="ts">
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
const banner = ref<MediaFile | null>(null)
const saving = ref(false)
const loading = ref(false)

watch(() => [open.value, props.categoryId] as const, async ([isOpen, id]) => {
  if (!isOpen) return

  if (id === null) {
    slug.value = ''
    translations.value = Object.fromEntries(props.languages.map(l => [l.code, {}]))
    banner.value = null
    return
  }

  loading.value = true
  try {
    const detail = await api.categories.get(id)
    slug.value = detail.slug
    // Start from every language, so one with nothing stored still gets a tab.
    translations.value = Object.fromEntries(
      props.languages.map(l => [l.code, { ...(detail.translations[l.code] ?? {}) }]))
    banner.value = detail.banner
  } finally {
    loading.value = false
  }
}, { immediate: true })

async function save() {
  saving.value = true
  try {
    const body = { slug: slug.value, translations: translations.value }

    const id = props.categoryId
      ?? (await api.categories.create(body) as { id: number }).id

    if (props.categoryId !== null) await api.categories.update(id, body)

    // The banner is a separate call: it is the media library's data, not the category's.
    await api.categories.setBanner(id, banner.value?.id ?? null)

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

        <MediaPicker
          v-model="banner"
          label="Banner"
          hint="Attached under the role 'banner', declared at startup as exactly one per category."
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
