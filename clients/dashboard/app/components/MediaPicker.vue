<script setup lang="ts">
/**
 * A box showing the currently selected file, with a dialog to pick a different one from the media
 * library or upload a new one.
 *
 * The picker lists `GET /api/media` — the library's own endpoint, which this application did not
 * write. Picking a file emits its id; the caller sends that id to
 * `PUT /api/categories/{id}/banner` (or `/products/{id}/primary`), where
 * `media.For(entity).SetAsync(role, id)` attaches it. The same file can serve several entities at
 * once, which is why picking from a library beats uploading per entity.
 */
defineProps<{
  label: string
  /** Shown under the box — what this role is for. */
  hint?: string
}>()

const selected = defineModel<MediaFile | null>({ default: null })

const api = useApi()
const toast = useToast()
const config = useRuntimeConfig()

const open = ref(false)
const uploading = ref(false)

const { data: library, refresh, status } = await useAsyncData(
  'media-library',
  () => api.media.list(),
  { immediate: false })

async function show() {
  open.value = true
  await refresh()
}

function choose(file: MediaFile) {
  selected.value = file
  open.value = false
}

function clear() {
  selected.value = null
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
    const uploaded = await api.media.upload(file)
    await refresh()
    choose(uploaded)
    toast.add({ title: 'Uploaded', color: 'success' })
  } catch (error) {
    toast.add({ title: 'Upload failed', description: String(error), color: 'error' })
  } finally {
    uploading.value = false
    input.value = ''
  }
}
</script>

<template>
  <UFormField :label="label" :help="hint">
    <div class="flex items-start gap-3">
      <!-- The box. -->
      <div
        class="flex size-24 shrink-0 items-center justify-center overflow-hidden rounded-lg border border-dashed border-accented bg-elevated/40"
      >
        <img
          v-if="selected"
          :src="mediaUrl(selected)"
          :alt="selected.altText ?? selected.fileName"
          class="size-full object-cover"
        >
        <UIcon v-else name="i-lucide-image-plus" class="size-7 text-dimmed" />
      </div>

      <div class="min-w-0 flex-1 space-y-2">
        <p v-if="selected" class="truncate text-sm font-medium">
          {{ selected.fileName }}
        </p>
        <p v-else class="text-sm text-muted">
          Nothing selected.
        </p>

        <div class="flex flex-wrap gap-2">
          <UButton
            icon="i-lucide-image"
            size="sm"
            variant="subtle"
            :label="selected ? 'Change' : 'Choose from library'"
            @click="show"
          />
          <UButton
            v-if="selected"
            icon="i-lucide-x"
            size="sm"
            variant="ghost"
            color="neutral"
            label="Clear"
            @click="clear"
          />
        </div>
      </div>
    </div>

    <UModal
      v-model:open="open"
      title="Media library"
      description="Every file in DotNetBoost.Media. One file can serve several entities at once."
      :ui="{ content: 'max-w-3xl' }"
    >
      <template #body>
        <div class="space-y-4">
          <div class="flex items-center justify-between gap-3">
            <p class="text-sm text-muted">
              {{ library?.total ?? 0 }} files
            </p>
            <UButton
              as="label"
              icon="i-lucide-upload"
              size="sm"
              variant="subtle"
              :loading="uploading"
              label="Upload new"
            >
              <input
                type="file"
                accept="image/*"
                class="hidden"
                @change="upload"
              >
            </UButton>
          </div>

          <div v-if="status === 'pending'" class="py-8 text-center text-muted">
            Loading…
          </div>

          <div v-else-if="!library?.items?.length" class="py-8 text-center text-muted">
            The library is empty. Upload a file to get started.
          </div>

          <div v-else class="grid max-h-96 grid-cols-2 gap-3 overflow-y-auto sm:grid-cols-4">
            <button
              v-for="file in library.items"
              :key="file.id"
              type="button"
              class="group space-y-1 text-start"
              @click="choose(file)"
            >
              <div
                class="overflow-hidden rounded-lg border-2 transition-colors"
                :class="selected?.id === file.id ? 'border-primary' : 'border-default group-hover:border-accented'"
              >
                <img
                  :src="mediaUrl(file)"
                  :alt="file.fileName"
                  class="h-24 w-full object-cover"
                >
              </div>
              <p class="truncate text-xs">
                {{ file.fileName }}
              </p>
            </button>
          </div>
        </div>
      </template>
    </UModal>
  </UFormField>
</template>
