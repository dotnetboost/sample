<script setup lang="ts">
/**
 * The dashboard's reading language, in the header of every page. It is sent as `Accept-Language`
 * on every call, so the API resolves it and returns translated content — and it flips the shell
 * to RTL when the language reads right to left.
 */
const api = useApi()
const language = useLanguage()

const { data: languages } = await useAsyncData('header-languages', () => api.multiLingual.languages())

const items = computed(() => (languages.value ?? [])
  .filter(l => l.isActive)
  .map(l => ({
    label: l.name,
    value: l.code,
    direction: isRtl(l.direction) ? 'rtl' : 'ltr'
  })))
</script>

<template>
  <USelect
    v-model="language"
    :items="items"
    value-key="value"
    icon="i-lucide-languages"
    color="neutral"
    variant="outline"
    class="w-32 sm:w-40"
    :content="{ align: 'end' }"
    aria-label="Reading language"
  >
    <template #item-label="{ item }">
      <span :dir="item.direction">{{ item.label }}</span>
    </template>

    <template #item-trailing="{ item }">
      <span class="font-mono text-xs text-muted">{{ item.value }}</span>
    </template>
  </USelect>
</template>
