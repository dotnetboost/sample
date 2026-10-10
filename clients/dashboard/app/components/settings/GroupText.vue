<script setup lang="ts">
import type { MediaOwner, SettingPropertySchema, Translations } from '~/types'

/**
 * A settings group's translated text in every language except the default one.
 *
 * Generic over the group, which it was not: it read `'branding'` for its schema and its labels,
 * and the endpoints it wrote to were a constant in `useApi`. It now takes the route and the
 * subject address, so any group whose schema reports a `translated` property gets this card and a
 * group with none gets nothing — decided by what the server says, not by which page is open.
 *
 * <b>Why the default is missing.</b> It is not stored here. The settings row holds it, the group
 * form above edits it, and it stays inside that group's ETag and audit trail. Every other language
 * is a row in the translations store. One value, one writer — the alternative was two boxes
 * writing the same thing and overwriting each other.
 *
 * <b>Why this card exists at all.</b> The generated settings form builds a field per value the API
 * returns. It has no idea one of them is translated, and no place to put several languages of it.
 */
const props = defineProps<{
  /** The group's route, which keys its schema and its field labels. */
  route: string
  /** Where its translations are filed, from the group's own response. */
  owner: MediaOwner
  /** The group's `[MultiLingual]` properties, already read from its schema by the form above. */
  translated: SettingPropertySchema[]
}>()

const emit = defineEmits<{ saved: [] }>()

const api = useApi()
const { notifySuccess, notifyFailure } = useNotify()
const { t } = useI18n()

const { languages, defaultLanguage, ensureLoaded } = useLanguages()

/**
 * The labels come from the same place the generated form takes them, because the two boxes edit
 * the same property in different languages. Taking both from `fieldLabel(route, name)` is what
 * keeps them identical when either is reworded.
 */

await ensureLoaded()

/** Everything except the default language, which the settings row owns. */
const translatable = computed(() =>
  languages.value.filter(l => !l.isDefault))

/**
 * Which of the group's properties are translated — handed down rather than fetched.
 *
 * `MultiLingualSchemaContributor` publishes `translated` for a property carrying
 * `[MultiLingual]`, so adding a second one to a group puts a box here with no change in this
 * component. It used to be a one-element literal naming `StoreName`, which is exactly why adding
 * `StoreDescription` showed a badge on the form above and nothing down here.
 *
 * The form above has already read the schema to draw its own inputs, so this takes the filtered
 * list as a prop instead of asking for the same document under a second cache key — which is what
 * it did, and what put two `GET /api/settings/branding/schema` in the network log per render.
 */
const fields = computed(() =>
  props.translated.map(p => ({ name: clrName(p.name), label: fieldLabel(props.route, p.name) })))

/**
 * The schema names a property as the GET body spells it — camelCase — while a translation row
 * keys on the CLR name. Capitalising the first character is the inverse of the JSON naming
 * policy for an ordinary identifier, which every property here is. A name beginning with an
 * acronym would not round-trip, and would need the schema to publish the CLR name outright.
 */
function clrName(wireName: string): string {
  return wireName.charAt(0).toUpperCase() + wireName.slice(1)
}

const draft = ref<Translations>({})
const original = ref('')
const loading = ref(false)
const saving = ref(false)

async function load() {
  loading.value = true

  try {
    const stored = await api.branding.text(props.owner)

    // Start from every translatable language, so one with nothing stored still gets a button.
    draft.value = Object.fromEntries(
      translatable.value.map(l => [l.code, { ...(stored[l.code] ?? {}) }]))

    original.value = JSON.stringify(draft.value)
  } catch (error) {
    notifyFailure(t('settings.groupText.toast.readFailed'), error)
  } finally {
    loading.value = false
  }
}

await load()

const dirty = computed(() => JSON.stringify(draft.value) !== original.value)

// Missing means "short of at least one translated property", which is what the badge claims.
// With one property that is the old behaviour; with two it is the only reading that stays true.
const missing = computed(() =>
  translatable.value.filter(language =>
    fields.value.some(field => !draft.value[language.code]?.[field.name]?.trim())).length)

async function save() {
  saving.value = true

  try {
    // The draft already IS languageCode -> (propertyName -> value), which is what the endpoint
    // takes. It used to be flattened to one property on the way out, which is what made a second
    // [MultiLingual] property impossible to save.
    await api.branding.setText(props.owner, draft.value)
    await load()

    notifySuccess(
      t('settings.groupText.toast.saved'),
      t('settings.groupText.toast.savedDescription')
    )

    emit('saved')
  } catch (error) {
    notifyFailure(t('settings.groupText.toast.saveFailed'), error)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <UPageCard
    variant="subtle"
    class="mt-4 sm:mt-6"
    :title="$t('settings.groupText.title')"
    :description="defaultLanguage
      ? $t('settings.groupText.descriptionWithDefault', { code: defaultLanguage.code })
      : $t('settings.groupText.descriptionPlain')"
  >
    <template #header>
      <div class="flex items-center justify-end gap-2">
        <UBadge
          v-if="missing"
          color="warning"
          variant="subtle"
          size="sm"
          :label="$t('settings.groupText.missing', missing)"
        />
        <UBadge
          v-else
          color="success"
          variant="subtle"
          size="sm"
          :label="$t('settings.groupText.covered')"
        />
      </div>
    </template>

    <div v-if="loading" class="py-4 text-center text-sm text-muted">
      {{ $t('common.loading') }}
    </div>

    <div v-else-if="!translatable.length" class="py-4 text-sm text-muted">
      {{ $t('settings.groupText.onlyOneLanguage') }}
    </div>

    <div v-else class="space-y-3">
      <TranslationEditor
        v-model="draft"
        :languages="translatable"
        :fields="fields"
      />

      <div class="flex items-center justify-between gap-4">
        <!-- `i18n-t` so the stored key stays a code span inside the sentence rather than becoming
             a second message a translator has to guess the placement of. -->
        <i18n-t
          scope="global"
          keypath="settings.groupText.note"
          tag="p"
          class="text-xs text-muted"
        >
          <template #subject>
            <!-- Named from the schema, so it stays true as properties are added or removed. -->
            <code>Branding · branding · {{ fields.map(f => f.name).join(', ') }}</code>
          </template>
        </i18n-t>
        <UButton
          :label="$t('settings.groupText.save')"
          :disabled="!dirty"
          :loading="saving"
          @click="save"
        />
      </div>
    </div>
  </UPageCard>
</template>
