<script setup lang="ts">
import { CircleCheck } from '@lucide/vue'
import type { Language } from '~/types'

/**
 * A button per language, the selected one filled with the accent, and a tick on every language
 * that already has something stored — the fastest way to see what is still untranslated without
 * opening each one. Shared by every per-language editor in the dashboard, so the tick means the
 * same thing in all of them.
 */
defineProps<{
  languages: Pick<Language, 'code' | 'name'>[]
  /** Whether a language has any value yet. */
  filled?: (code: string) => boolean
  size?: 'sm' | 'default'
}>()

const active = defineModel<string>({ required: true })
</script>

<template>
  <ToggleGroup
    :model-value="active"
    type="single"
    variant="outline"
    :size="size ?? 'sm'"
    :spacing="1"
    class="flex-wrap justify-start"
    @update:model-value="value => { if (value) active = value as string }"
  >
    <ToggleGroupItem
      v-for="language in languages"
      :key="language.code"
      :value="language.code"
      class="gap-1.5 data-[state=on]:border-primary data-[state=on]:bg-primary data-[state=on]:text-primary-foreground"
    >
      {{ language.name }}
      <CircleCheck v-if="filled?.(language.code)" class="size-3.5" />
      <span class="text-[10px] opacity-60">{{ language.code }}</span>
    </ToggleGroupItem>
  </ToggleGroup>
</template>
