<script setup lang="ts">
import type { Language } from '~/types'

/**
 * Per-language text for one entity: a button per available language, and the fields for whichever
 * is selected.
 *
 * The shape is `languageCode → (propertyName → value)` — exactly what
 * `IMultiLingualManager.For(entity).GetAllAsync()` returns and what `Set(language, values)`
 * accepts, so the editor hands the API back the same structure it was given.
 *
 * A button shows a dot when that language has any value, which is the fastest way to see what is
 * still untranslated without opening each one.
 */
const props = defineProps<{
  languages: Language[]
  /** Property names to edit, in order — e.g. ['Name', 'Description']. */
  fields: { name: string, label: string, textarea?: boolean }[]
}>()

const model = defineModel<Record<string, Record<string, string | null>>>({ required: true })

const active = ref(props.languages[0]?.code ?? 'en')

function values(code: string) {
  model.value[code] ??= {}
  return model.value[code]!
}

const hasContent = (code: string) =>
  Object.values(model.value[code] ?? {}).some(v => (v ?? '').trim().length > 0)

const activeIsRtl = computed(() =>
  isRtl(props.languages.find(l => l.code === active.value)?.direction))
</script>

<template>
  <div class="space-y-3">
    <!-- One button per available language. -->
    <LanguagePills v-model="active" :languages="languages" :filled="hasContent" />

    <!-- Fields for the selected language only. -->
    <FieldGroup class="gap-4 rounded-lg border p-4">
      <Field v-for="field in fields" :key="field.name">
        <FieldLabel :for="`translation-${active}-${field.name}`">
          {{ field.label }}
        </FieldLabel>
        <Textarea
          v-if="field.textarea"
          :id="`translation-${active}-${field.name}`"
          v-model="values(active)[field.name] as string"
          :dir="activeIsRtl ? 'rtl' : 'ltr'"
          :rows="3"
        />
        <Input
          v-else
          :id="`translation-${active}-${field.name}`"
          v-model="values(active)[field.name] as string"
          :dir="activeIsRtl ? 'rtl' : 'ltr'"
        />
      </Field>

      <FieldDescription class="text-xs">
        {{ $t('translationFields.note') }}
      </FieldDescription>
    </FieldGroup>
  </div>
</template>
