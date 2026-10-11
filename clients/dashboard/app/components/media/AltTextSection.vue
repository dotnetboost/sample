<script setup lang="ts">
import type { MediaTextValues } from '~/types'

/**
 * Alt text and caption for one file, in every language — the sample's one addition to
 * DotNetBoost.Media's own file details.
 *
 * None of this is media data. `MediaFile` has no alt-text field, and the media library never
 * learns that these rows exist: they are DotNetBoost.MultiLingual translations filed under the
 * subject "MediaFile" and this file's own GUID, read and written through that library's own
 * `/api/translations` endpoints. Two libraries acting on one subject, and the only thing joining
 * them is the subject name — which the API declares in one place and exposes deliberately.
 *
 * A language button is ticked when that language has something stored, which is the fastest way to
 * see what is still missing — and alt text missing in a language is an accessibility defect, not a
 * cosmetic one.
 */
const props = defineProps<{
  mediaFileId: string
}>()

const api = useApi()
const { notifySuccess, notifyFailure } = useNotify()
const { t } = useI18n()

// The one shared language list, awaited here because the rows below are drawn from it and a
// row per language of a list that has not landed yet is no rows at all.
const { languages, ready } = useLanguageList()
await ready()

const draft = ref<Record<string, MediaTextValues>>({})
const original = ref('')
const active = ref('')
const loading = ref(false)
const saving = ref(false)

async function load() {
  loading.value = true

  try {
    const stored = await api.mediaText.get(props.mediaFileId)

    // A row per language the system knows about, whether or not anything is stored for it: the
    // buttons above are drawn from this, and a language with nothing yet is exactly what the
    // editor is for.
    const next: Record<string, MediaTextValues> = {}

    for (const language of languages.value) {
      next[language.code] = stored[language.code] ?? { altText: null, caption: null }
    }

    draft.value = next
    original.value = JSON.stringify(next)
    active.value = languages.value[0]?.code ?? ''
  } catch (error) {
    notifyFailure(t('media.altText.toast.readFailed'), error)
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
  languages.value.filter(l => !draft.value[l.code]?.altText?.trim()).length)

const activeIsRtl = computed(() =>
  isRtl(languages.value.find(l => l.code === active.value)?.direction))

async function save() {
  saving.value = true

  try {
    await api.mediaText.set(props.mediaFileId, draft.value)
    original.value = JSON.stringify(draft.value)
    notifySuccess(t('media.altText.toast.saved'))
  } catch (error) {
    notifyFailure(t('media.altText.toast.saveFailed'), error)
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
          {{ $t('media.altText.title') }}
        </p>
        <p class="text-xs text-muted">
          {{ $t('media.altText.subtitle') }}
        </p>
      </div>

      <UBadge
        v-if="missing"
        color="warning"
        variant="subtle"
        size="sm"
        :label="$t('media.altText.missing', missing)"
      />
      <UBadge
        v-else
        color="success"
        variant="subtle"
        size="sm"
        :label="$t('media.altText.covered')"
      />
    </div>

    <div v-if="loading" class="py-4 text-center text-sm text-muted">
      {{ $t('common.loading') }}
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
        <UFormField
          :label="$t('media.altText.altText.label')"
          :description="$t('media.altText.altText.description')"
        >
          <UInput
            v-model="draft[active]!.altText as string"
            :dir="activeIsRtl ? 'rtl' : 'ltr'"
            class="w-full"
          />
        </UFormField>

        <UFormField :label="$t('media.altText.caption')">
          <UInput
            v-model="draft[active]!.caption as string"
            :dir="activeIsRtl ? 'rtl' : 'ltr'"
            class="w-full"
          />
        </UFormField>

        <div class="flex items-center justify-between gap-2">
          <p class="text-xs text-muted">
            {{ $t('media.altText.note') }}
          </p>
          <UButton
            size="xs"
            :label="$t('media.altText.save')"
            :disabled="!dirty"
            :loading="saving"
            @click="save"
          />
        </div>
      </div>
    </template>
  </div>
</template>
