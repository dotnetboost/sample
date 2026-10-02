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
const { notifySuccess, notifyFailure } = useNotify()
const { t } = useI18n()

const { languages, defaultLanguage, ensureLoaded } = useLanguages()

/**
 * The branding group's metadata, borrowed for one thing: the label on the field below.
 *
 * It has to read `Store name` exactly as the generated form above does, because the two boxes edit
 * the same property in different languages. Taking the label from the same place the form takes it
 * is what keeps them identical when either is reworded.
 */
const brandingGroup = findSettingsGroup('branding')!

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
    notifyFailure(t('settings.brandingText.toast.readFailed'), error)
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

    notifySuccess(
      t('settings.brandingText.toast.saved'),
      t('settings.brandingText.toast.savedDescription')
    )

    emit('saved')
  } catch (error) {
    notifyFailure(t('settings.brandingText.toast.saveFailed'), error)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <UPageCard
    variant="subtle"
    class="mt-4 sm:mt-6"
    :title="$t('settings.brandingText.title')"
    :description="defaultLanguage
      ? $t('settings.brandingText.descriptionWithDefault', { code: defaultLanguage.code })
      : $t('settings.brandingText.descriptionPlain')"
  >
    <template #header>
      <div class="flex items-center justify-end gap-2">
        <UBadge
          v-if="missing"
          color="warning"
          variant="subtle"
          size="sm"
          :label="$t('settings.brandingText.missing', missing)"
        />
        <UBadge
          v-else
          color="success"
          variant="subtle"
          size="sm"
          :label="$t('settings.brandingText.covered')"
        />
      </div>
    </template>

    <div v-if="loading" class="py-4 text-center text-sm text-muted">
      {{ $t('common.loading') }}
    </div>

    <div v-else-if="!translatable.length" class="py-4 text-sm text-muted">
      {{ $t('settings.brandingText.onlyOneLanguage') }}
    </div>

    <div v-else class="space-y-3">
      <TranslationEditor
        v-model="draft"
        :languages="translatable"
        :fields="[{ name: 'StoreName', label: fieldLabel(brandingGroup, 'storeName') }]"
      />

      <div class="flex items-center justify-between gap-4">
        <!-- `i18n-t` so the stored key stays a code span inside the sentence rather than becoming
             a second message a translator has to guess the placement of. -->
        <i18n-t
          scope="global"
          keypath="settings.brandingText.note"
          tag="p"
          class="text-xs text-muted"
        >
          <template #subject>
            <code>Branding · branding · StoreName</code>
          </template>
        </i18n-t>
        <UButton
          :label="$t('settings.brandingText.save')"
          :disabled="!dirty"
          :loading="saving"
          @click="save"
        />
      </div>
    </div>
  </UPageCard>
</template>
