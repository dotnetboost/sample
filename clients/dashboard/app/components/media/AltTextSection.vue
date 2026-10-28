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
        <p class="text-sm font-medium">
          {{ $t('media.altText.title') }}
        </p>
        <p class="text-xs text-muted-foreground">
          {{ $t('media.altText.subtitle') }}
        </p>
      </div>

      <Badge v-if="missing" class="bg-warning/15 text-warning">
        {{ $t('media.altText.missing', missing) }}
      </Badge>
      <Badge v-else class="bg-success/15 text-success">
        {{ $t('media.altText.covered') }}
      </Badge>
    </div>

    <div v-if="loading" class="flex items-center justify-center gap-2 py-4 text-sm text-muted-foreground">
      <Spinner />
      {{ $t('common.loading') }}
    </div>

    <template v-else>
      <LanguagePills v-model="active" :languages="languages" :filled="hasContent" />

      <FieldGroup v-if="draft[active]" class="gap-4 rounded-lg border p-3">
        <Field>
          <FieldLabel :for="`alt-${active}`">
            {{ $t('media.altText.altText.label') }}
          </FieldLabel>
          <Input
            :id="`alt-${active}`"
            v-model="draft[active]!.altText as string"
            :dir="activeIsRtl ? 'rtl' : 'ltr'"
          />
          <FieldDescription>{{ $t('media.altText.altText.description') }}</FieldDescription>
        </Field>

        <Field>
          <FieldLabel :for="`caption-${active}`">
            {{ $t('media.altText.caption') }}
          </FieldLabel>
          <Input
            :id="`caption-${active}`"
            v-model="draft[active]!.caption as string"
            :dir="activeIsRtl ? 'rtl' : 'ltr'"
          />
        </Field>

        <div class="flex items-center justify-between gap-2">
          <p class="text-xs text-muted-foreground">
            {{ $t('media.altText.note') }}
          </p>
          <Button size="xs" :disabled="!dirty || saving" @click="save">
            <Spinner v-if="saving" />
            {{ $t('media.altText.save') }}
          </Button>
        </div>
      </FieldGroup>
    </template>
  </div>
</template>
