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
    <div class="flex flex-wrap gap-1.5">
      <UButton
        v-for="language in languages"
        :key="language.code"
        :label="language.name"
        :color="active === language.code ? 'primary' : 'neutral'"
        :variant="active === language.code ? 'solid' : 'outline'"
        size="sm"
        @click="active = language.code"
      >
        <template #trailing>
          <span class="flex items-center gap-1">
            <UIcon
              v-if="hasContent(language.code)"
              name="i-lucide-circle-check"
              class="size-3.5"
            />
            <span class="font-mono text-[10px] opacity-60">{{ language.code }}</span>
          </span>
        </template>
      </UButton>
    </div>

    <!-- Fields for the selected language only. -->
    <div class="space-y-3 rounded-lg border border-default p-4">
      <UFormField
        v-for="field in fields"
        :key="field.name"
        :label="field.label"
      >
        <UTextarea
          v-if="field.textarea"
          v-model="values(active)[field.name] as string"
          :dir="activeIsRtl ? 'rtl' : 'ltr'"
          :rows="3"
          class="w-full"
        />
        <UInput
          v-else
          v-model="values(active)[field.name] as string"
          :dir="activeIsRtl ? 'rtl' : 'ltr'"
          class="w-full"
        />
      </UFormField>

      <p class="text-xs text-muted">
        {{ $t('translationFields.note') }}
      </p>
    </div>
  </div>
</template>
