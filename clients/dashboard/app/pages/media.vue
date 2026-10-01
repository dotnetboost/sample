<script setup lang="ts">
useHead({ title: 'Media — DotNetBoost Sample' })

const api = useApi()
const toast = useToast()
const config = useRuntimeConfig()

/**
 * The library listing joined to its alt text.
 *
 * Two calls, deliberately: `GET /api/media` is the media library's own endpoint and knows nothing
 * about translations, so the text comes from this application's `/api/media-text/resolve`, which
 * resolves every file in one query. That join is the composition, at list scale.
 */
const { data: library, refresh } = await useAsyncData('media-page', async () => {
  const page = await api.media.list()
  if (!page.items.length) return page

  const text = await api.mediaText.resolve(page.items.map(f => f.id))

  return {
    ...page,
    items: page.items.map(file => ({
      ...file,
      altText: text[file.id]?.altText ?? null,
      caption: text[file.id]?.caption ?? null
    }))
  }
})
const { data: languages } = await useAsyncData('media-languages', () => api.multiLingual.languages())

const language = useLanguage()
watch(language, () => refresh())

const editing = ref<MediaFile | null>(null)
const draft = ref<Record<string, { altText: string | null, caption: string | null }>>({})
const activeLanguage = ref('en')
const saving = ref(false)
const uploading = ref(false)

const confirming = ref<MediaFile | null>(null)
const deleting = ref(false)

async function open(file: MediaFile) {
  editing.value = file
  draft.value = {}
  activeLanguage.value = languages.value?.[0]?.code ?? 'en'

  const detail = await api.mediaText.get(file.id)

  for (const language of languages.value ?? []) {
    const stored = detail.byLanguage[language.code] ?? {}
    draft.value[language.code] = {
      altText: stored.AltText ?? null,
      caption: stored.Caption ?? null
    }
  }
}

async function save() {
  if (!editing.value) return

  saving.value = true
  try {
    await api.mediaText.set(editing.value.id, draft.value)
    await refresh()
    toast.add({ title: 'Saved', color: 'success' })
    editing.value = null
  } catch (error) {
    toast.add({ title: 'Save failed', description: String(error), color: 'error' })
  } finally {
    saving.value = false
  }
}

async function upload(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  if (file.size > Number(config.public.maxUploadBytes)) {
    toast.add({ title: 'File too large', color: 'error' })
    input.value = ''
    return
  }

  uploading.value = true
  try {
    await api.media.upload(file)
    await refresh()
    toast.add({ title: 'Uploaded', color: 'success' })
  } catch (error) {
    toast.add({ title: 'Upload failed', description: String(error), color: 'error' })
  } finally {
    uploading.value = false
    input.value = ''
  }
}

async function confirmDelete() {
  if (!confirming.value) return

  deleting.value = true
  try {
    await api.media.remove(confirming.value.id)
    await refresh()
    toast.add({ title: 'Deleted', color: 'success' })
    confirming.value = null
  } catch (error) {
    toast.add({ title: 'Could not delete', description: String(error), color: 'error' })
  } finally {
    deleting.value = false
  }
}

const hasText = (code: string) => {
  const values = draft.value[code]
  return Boolean(values?.altText?.trim() || values?.caption?.trim())
}

const activeIsRtl = computed(() =>
  isRtl(languages.value?.find(l => l.code === activeLanguage.value)?.direction))
</script>

<template>
  <UDashboardPanel id="media">
    <template #header>
      <UDashboardNavbar title="Media">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>

        <template #right>
          <LanguageSelect />
        </template>
      </UDashboardNavbar>

      <UDashboardToolbar>
        <template #right>
          <UButton
            as="label"
            icon="i-lucide-upload"
            size="sm"
            :loading="uploading"
            label="Upload"
          >
            <input
              type="file"
              accept="image/*"
              class="hidden"
              @change="upload"
            >
          </UButton>
        </template>
      </UDashboardToolbar>
    </template>

    <template #body>
      <UPageCard
        title="The library, and its alt text"
        description="Each file is a row in DotNetBoost.Media; its alt text and caption are rows in DotNetBoost.MultiLingual, filed under the subject &quot;MediaFile&quot; and the file's own GUID. MediaFile ships with no alt-text field at all — which is exactly why this pairing is worth demonstrating, and why it needs no schema change in either library."
        variant="naked"
      />

      <UCard v-if="!library?.items?.length">
        <p class="text-muted">
          No files yet. Upload one, or add an image from the Catalog editor.
        </p>
      </UCard>

      <div v-else class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        <UCard
          v-for="file in library.items"
          :key="file.id"
          :ui="{ body: 'p-0', footer: 'p-3' }"
        >
          <img
            :src="mediaUrl(file)"
            :alt="file.altText ?? ''"
            class="h-36 w-full object-cover"
          >

          <div class="space-y-1 p-3">
            <p class="truncate text-sm font-medium">
              {{ file.fileName }}
            </p>
            <p v-if="file.altText" class="truncate text-xs text-muted">
              {{ file.altText }}
            </p>
            <p v-else class="text-xs text-warning">
              no alt text in this language
            </p>
          </div>

          <template #footer>
            <div class="flex gap-2">
              <UButton
                icon="i-lucide-pencil"
                size="sm"
                variant="subtle"
                class="flex-1"
                label="Alt text"
                @click="open(file)"
              />
              <UButton
                icon="i-lucide-trash-2"
                size="sm"
                variant="ghost"
                color="error"
                @click="confirming = file"
              />
            </div>
          </template>
        </UCard>
      </div>

      <USlideover
        :open="editing !== null"
        title="Alt text and caption"
        description="One row per language, written straight to the translations store."
        @update:open="editing = null"
      >
        <template #body>
          <div class="space-y-4">
            <img
              v-if="editing"
              :src="mediaUrl(editing)"
              :alt="editing.fileName"
              class="h-40 w-full rounded-lg border border-default object-cover"
            >

            <!-- A button per available language, as in the Catalog editor. -->
            <div class="flex flex-wrap gap-1.5">
              <UButton
                v-for="option in languages"
                :key="option.code"
                :label="option.name"
                :color="activeLanguage === option.code ? 'primary' : 'neutral'"
                :variant="activeLanguage === option.code ? 'solid' : 'outline'"
                size="sm"
                @click="activeLanguage = option.code"
              >
                <template #trailing>
                  <span class="flex items-center gap-1">
                    <UIcon v-if="hasText(option.code)" name="i-lucide-circle-check" class="size-3.5" />
                    <span class="font-mono text-[10px] opacity-60">{{ option.code }}</span>
                  </span>
                </template>
              </UButton>
            </div>

            <div v-if="draft[activeLanguage]" class="space-y-3 rounded-lg border border-default p-4">
              <UFormField label="Alt text" help="Required for accessibility, and different in every language.">
                <UInput
                  v-model="draft[activeLanguage]!.altText as string"
                  :dir="activeIsRtl ? 'rtl' : 'ltr'"
                  class="w-full"
                />
              </UFormField>

              <UFormField label="Caption">
                <UInput
                  v-model="draft[activeLanguage]!.caption as string"
                  :dir="activeIsRtl ? 'rtl' : 'ltr'"
                  class="w-full"
                />
              </UFormField>

              <p class="text-xs text-muted">
                Leaving a field empty removes the stored row rather than storing an empty string —
                that is the translation store's own contract.
              </p>
            </div>
          </div>
        </template>

        <template #footer>
          <div class="flex w-full justify-end gap-2">
            <UButton
              label="Cancel"
              color="neutral"
              variant="ghost"
              @click="editing = null"
            />
            <UButton :loading="saving" label="Save" @click="save" />
          </div>
        </template>
      </USlideover>

      <ConfirmModal
        :open="confirming !== null"
        :title="`Delete ${confirming?.fileName ?? ''}?`"
        description="The file is removed from the library. Its alt text is NOT removed automatically — the media library has no idea the translation rows exist. That is the honest cost of having no integration package."
        confirm-label="Delete"
        :loading="deleting"
        @update:open="confirming = null"
        @confirm="confirmDelete"
      />
    </template>
  </UDashboardPanel>
</template>
