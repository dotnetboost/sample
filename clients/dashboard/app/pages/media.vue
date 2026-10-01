<script setup lang="ts">
import type { MediaTextValues } from '~/composables/useApi'

const api = useApi()
const toast = useToast()

const { data: products, refresh } = await useAsyncData('media-products', () => api.catalog.products())
const { data: languages } = await useAsyncData('media-languages', () => api.multiLingual.languages())

const images = computed(() =>
  (products.value ?? []).flatMap(p => p.images.map(image => ({ ...image, product: p.name }))))

const editing = ref<string | null>(null)
const draft = ref<Record<string, MediaTextValues>>({})
const saving = ref(false)

async function open(mediaFileId: string) {
  editing.value = mediaFileId
  draft.value = {}

  const detail = await api.mediaText.get(mediaFileId)

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
    await api.mediaText.set(editing.value, draft.value)
    await refresh()
    toast.add({ title: 'Saved', color: 'success' })
    editing.value = null
  } catch (error) {
    toast.add({ title: 'Save failed', description: String(error), color: 'error' })
  } finally {
    saving.value = false
  }
}

const isRtlLanguage = (code: string) =>
  isRtl(languages.value?.find(l => l.code === code)?.direction)
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-semibold">
        Media & alt text
      </h1>
      <p class="mt-1 max-w-3xl text-muted">
        The composition, editable. Each file is a row in DotNetBoost.Media; its alt text and
        caption are rows in DotNetBoost.MultiLingual, filed under the subject
        <code>"MediaFile"</code> and the file's own GUID. Neither library knows the other exists.
      </p>
    </div>

    <UAlert
      icon="i-lucide-accessibility"
      variant="subtle"
      color="neutral"
      title="Why alt text is the natural pairing"
      description="Alt text is required for accessibility and is inherently per-language — so it belongs to the translations store, not to a new column on the media table. MediaFile ships with no alt-text field at all, which is what makes this worth demonstrating."
    />

    <UCard v-if="!images.length">
      <p class="text-muted">
        No images yet. Upload one from the Catalog page.
      </p>
    </UCard>

    <div v-else class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      <UCard
        v-for="image in images"
        :key="image.id"
        class="overflow-hidden"
      >
        <img
          :src="image.url"
          :alt="image.altText ?? ''"
          class="-mx-6 -mt-6 mb-4 h-40 w-[calc(100%+3rem)] object-cover"
        >

        <p class="truncate text-sm font-medium">
          {{ image.fileName }}
        </p>
        <p class="text-xs text-muted">
          {{ image.product }}
        </p>

        <div class="mt-3 space-y-1 text-xs">
          <p v-if="image.altText" class="text-muted">
            <span class="font-medium text-default">alt:</span> {{ image.altText }}
          </p>
          <p v-else class="text-warning">
            no alt text in this language
          </p>
          <p v-if="image.caption" class="text-muted">
            <span class="font-medium text-default">caption:</span> {{ image.caption }}
          </p>
        </div>

        <template #footer>
          <UButton
            icon="i-lucide-pencil"
            size="sm"
            variant="subtle"
            block
            label="Edit all languages"
            @click="open(image.id)"
          />
        </template>
      </UCard>
    </div>

    <UModal
      :open="editing !== null"
      title="Alt text and caption"
      description="One row per language, written straight to the translations store."
      @update:open="editing = null"
    >
      <template #body>
        <div class="space-y-5">
          <div
            v-for="language in languages"
            :key="language.code"
            class="space-y-2"
          >
            <div class="flex items-center gap-2">
              <UBadge variant="subtle" size="sm">
                {{ language.code }}
              </UBadge>
              <span class="text-sm font-medium">{{ language.name }}</span>
              <UBadge
                v-if="isRtlLanguage(language.code)"
                color="neutral"
                variant="subtle"
                size="sm"
              >
                RTL
              </UBadge>
            </div>

            <UInput
              v-if="draft[language.code]"
              v-model="draft[language.code]!.altText"
              :dir="isRtlLanguage(language.code) ? 'rtl' : 'ltr'"
              placeholder="Alt text"
              class="w-full"
            />
            <UInput
              v-if="draft[language.code]"
              v-model="draft[language.code]!.caption"
              :dir="isRtlLanguage(language.code) ? 'rtl' : 'ltr'"
              placeholder="Caption"
              class="w-full"
            />
          </div>

          <p class="text-xs text-muted">
            Leaving a field empty removes the stored row rather than storing an empty string —
            that is the translation store's own contract.
          </p>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            variant="ghost"
            color="neutral"
            label="Cancel"
            @click="editing = null"
          />
          <UButton
            :loading="saving"
            label="Save"
            @click="save"
          />
        </div>
      </template>
    </UModal>
  </div>
</template>
