<script setup lang="ts">
import type { Translations } from '~/types'

/**
 * The branding group's text in every language except the default one.
 *
 * <b>Why the default is missing.</b> It is not stored here. The settings row holds it, the group
 * form above edits it, and it stays inside that group's ETag and audit trail. Every other language
 * is a row in the translations store. One value, one writer — the alternative was two boxes
 * writing the same thing and overwriting each other.
 *
 * <b>Why this card exists at all.</b> The generated settings form builds a field per value the API
 * returns. It has no idea one of them is translated, and no place to put several languages of it.
 */
const emit = defineEmits<{ saved: [] }>()

const api = useApi()
const toast = useToast()

const { languages, defaultLanguage, ensureLoaded } = useLanguages()
await ensureLoaded()

/** Everything except the default language, which the settings row owns. */
const translatable = computed(() =>
  languages.value.filter(l => !l.isDefault))

const draft = ref<Translations>({})
const original = ref('')
const loading = ref(false)
const saving = ref(false)

async function load() {
  loading.value = true

  try {
    const stored = await api.branding.text()

    // Start from every translatable language, so one with nothing stored still gets a button.
    draft.value = Object.fromEntries(
      translatable.value.map(l => [l.code, { ...(stored[l.code] ?? {}) }]))

    original.value = JSON.stringify(draft.value)
  } catch (error) {
    toast.add({
      title: 'Could not read the translations',
      description: problemMessage(error),
      icon: 'i-lucide-triangle-alert',
      color: 'error'
    })
  } finally {
    loading.value = false
  }
}

await load()

const dirty = computed(() => JSON.stringify(draft.value) !== original.value)

const missing = computed(() =>
  translatable.value.filter(l => !draft.value[l.code]?.StoreName?.trim()).length)

async function save() {
  saving.value = true

  try {
    // Only StoreName is translated today. Sending the whole draft keyed by language matches the
    // endpoint, which takes languageCode -> value for the group's translated text.
    const values = Object.fromEntries(
      Object.entries(draft.value).map(([code, properties]) => [code, properties.StoreName ?? null]))

    await api.branding.setText(values)
    await load()

    toast.add({
      title: 'Translations saved',
      description: 'Reading in those languages now returns them.',
      icon: 'i-lucide-check',
      color: 'success'
    })

    emit('saved')
  } catch (error) {
    toast.add({
      title: 'Could not save the translations',
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
  <UPageCard
    variant="subtle"
    class="mt-4 sm:mt-6"
    title="Store name in other languages"
    :description="defaultLanguage
      ? `The default language (${defaultLanguage.code}) is the value in the form above — it lives in the settings row. These are translation rows, and a language with nothing stored falls back to it.`
      : 'These are translation rows. A language with nothing stored falls back to the default.'"
  >
    <template #header>
      <div class="flex items-center justify-end gap-2">
        <UBadge
          v-if="missing"
          color="warning"
          variant="subtle"
          size="sm"
          :label="`${missing} language${missing === 1 ? '' : 's'} not translated`"
        />
        <UBadge
          v-else
          color="success"
          variant="subtle"
          size="sm"
          label="Every language covered"
        />
      </div>
    </template>

    <div v-if="loading" class="py-4 text-center text-sm text-muted">
      Loading…
    </div>

    <div v-else-if="!translatable.length" class="py-4 text-sm text-muted">
      There is only one language, so there is nothing to translate yet. Add another on the
      Languages page.
    </div>

    <div v-else class="space-y-3">
      <TranslationEditor
        v-model="draft"
        :languages="translatable"
        :fields="[{ name: 'StoreName', label: 'Store name' }]"
      />

      <div class="flex items-center justify-between gap-4">
        <p class="text-xs text-muted">
          Stored as <code>Branding · branding · StoreName</code> rows, one per language — the same
          subject the logo is attached to.
        </p>
        <UButton
          label="Save translations"
          :disabled="!dirty"
          :loading="saving"
          @click="save"
        />
      </div>
    </div>
  </UPageCard>
</template>
