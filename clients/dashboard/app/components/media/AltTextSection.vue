<script setup lang="ts">
import type { Language, MediaTextValues } from '~/types'

/**
 * Alt text and caption for one file, in every language — the sample's one addition to
 * DotNetBoost.Media's own file details.
 *
 * None of this is media data. `MediaFile` has no alt-text field, and the media library never
 * learns that these rows exist: they are DotNetBoost.MultiLingual translations filed under the
 * subject "MediaFile" and this file's own GUID, written through the application's
 * `/api/media-text` endpoints. Two libraries acting on one subject, joined in application code.
 *
 * A language button is ticked when that language has something stored, which is the fastest way to
 * see what is still missing — and alt text missing in a language is an accessibility defect, not a
 * cosmetic one.
 */
const props = defineProps<{
  mediaFileId: string
}>()

const api = useApi()
const toast = useToast()

const { data: languages } = await useAsyncData('alt-text-languages', () => api.multiLingual.languages())

const draft = ref<Record<string, MediaTextValues>>({})
const original = ref('')
const active = ref('')
const loading = ref(false)
const saving = ref(false)

async function load() {
  loading.value = true

  try {
    const detail = await api.mediaText.get(props.mediaFileId)

    const next: Record<string, MediaTextValues> = {}

    for (const language of languages.value ?? []) {
      const stored = detail.byLanguage[language.code] ?? {}
      next[language.code] = {
        altText: stored.AltText ?? null,
        caption: stored.Caption ?? null
      }
    }

    draft.value = next
    original.value = JSON.stringify(next)
    active.value = languages.value?.[0]?.code ?? ''
  } catch (error) {
    toast.add({
      title: 'Could not read the alt text',
      description: problemMessage(error),
      icon: 'i-lucide-triangle-alert',
      color: 'error'
    })
  } finally {
    loading.value = false
  }
}

watch(() => props.mediaFileId, load, { immediate: true })

const dirty = computed(() => JSON.stringify(draft.value) !== original.value)

const hasContent = (code: string) => {
  const values = draft.value[code]
  return Boolean(values?.altText?.trim() || values?.caption?.trim())
}

const missing = computed(() =>
  (languages.value ?? []).filter(l => !draft.value[l.code]?.altText?.trim()).length)

const activeIsRtl = computed(() =>
  isRtl((languages.value as Language[] | null)?.find(l => l.code === active.value)?.direction))

async function save() {
  saving.value = true

  try {
    await api.mediaText.set(props.mediaFileId, draft.value)
    original.value = JSON.stringify(draft.value)
    toast.add({ title: 'Alt text saved', icon: 'i-lucide-check', color: 'success' })
  } catch (error) {
    toast.add({
      title: 'Could not save the alt text',
      description: problemMessage(error),
      icon: 'i-lucide-triangle-alert',
      color: 'error'
    })
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="space-y-3">
    <div class="flex items-center justify-between gap-2">
      <div>
        <p class="text-sm font-medium text-highlighted">
          Alt text and caption
        </p>
        <p class="text-xs text-muted">
          Stored by DotNetBoost.MultiLingual, not by the media library.
        </p>
      </div>

      <UBadge
        v-if="missing"
        color="warning"
        variant="subtle"
        size="sm"
        :label="`${missing} language${missing === 1 ? '' : 's'} without alt text`"
      />
      <UBadge
        v-else
        color="success"
        variant="subtle"
        size="sm"
        label="Every language covered"
      />
    </div>

    <div v-if="loading" class="py-4 text-center text-sm text-muted">
      Loading…
    </div>

    <template v-else>
      <div class="flex flex-wrap gap-1.5">
        <UButton
          v-for="option in languages"
          :key="option.code"
          :label="option.name"
          :color="active === option.code ? 'primary' : 'neutral'"
          :variant="active === option.code ? 'solid' : 'outline'"
          size="xs"
          @click="active = option.code"
        >
          <template #trailing>
            <span class="flex items-center gap-1">
              <UIcon v-if="hasContent(option.code)" name="i-lucide-circle-check" class="size-3" />
              <span class="font-mono text-[10px] opacity-60">{{ option.code }}</span>
            </span>
          </template>
        </UButton>
      </div>

      <div v-if="draft[active]" class="space-y-3 rounded-lg border border-default p-3">
        <UFormField label="Alt text" description="Required for accessibility, and different in every language.">
          <UInput
            v-model="draft[active]!.altText as string"
            :dir="activeIsRtl ? 'rtl' : 'ltr'"
            class="w-full"
          />
        </UFormField>

        <UFormField label="Caption">
          <UInput
            v-model="draft[active]!.caption as string"
            :dir="activeIsRtl ? 'rtl' : 'ltr'"
            class="w-full"
          />
        </UFormField>

        <div class="flex items-center justify-between gap-2">
          <p class="text-xs text-muted">
            Clearing a field removes the stored row rather than storing an empty string.
          </p>
          <UButton
            size="xs"
            label="Save alt text"
            :disabled="!dirty"
            :loading="saving"
            @click="save"
          />
        </div>
      </div>
    </template>
  </div>
</template>
