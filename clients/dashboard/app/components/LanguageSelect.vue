<script setup lang="ts">
import { Languages } from '@lucide/vue'

/**
 * The dashboard's reading language, in the header of every page. It is sent as `Accept-Language`
 * on every call, so the API resolves it and returns translated content — it flips the shell to RTL
 * when the language reads right to left, and `plugins/ui-language.ts` moves the dashboard's own
 * labels with it.
 *
 * This one control is the whole language story. A second switcher for the chrome would be a second
 * answer to the same question, and two answers can disagree.
 */
const language = useLanguage()

const { languages } = useShellLocale()

const items = computed(() => languages.value
  .filter(l => l.isActive)
  .map(l => ({
    label: l.name,
    value: l.code,
    direction: isRtl(l.direction) ? 'rtl' : 'ltr'
  })))

// Named in the trigger by itself. Left to reka-ui, the trigger would repeat the whole item — the
// code beside the name included — which is what the open list is for.
const current = computed(() => items.value.find(item => item.value === language.value))
</script>

<template>
  <Select v-model="language">
    <SelectTrigger size="sm" class="w-32 sm:w-40" :aria-label="$t('readingLanguage.label')">
      <Languages class="text-muted-foreground" />
      <SelectValue>
        <span :dir="current?.direction">{{ current?.label ?? language }}</span>
      </SelectValue>
    </SelectTrigger>

    <SelectContent position="popper" align="end">
      <SelectItem
        v-for="item in items"
        :key="item.value"
        :value="item.value"
        :text-value="item.label"
      >
        <span :dir="item.direction">{{ item.label }}</span>
        <span class="ms-auto ps-3 font-mono text-xs text-muted-foreground">{{ item.value }}</span>
      </SelectItem>
    </SelectContent>
  </Select>
</template>
