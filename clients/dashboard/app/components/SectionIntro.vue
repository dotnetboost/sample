<script setup lang="ts">
/**
 * A heading and the sentence under it, at the top of a page or of a section of one — what
 * `UPageCard variant="naked"` drew under Nuxt UI. Anything in the default slot goes on the far
 * side, for the one action that belongs to the section rather than to the page.
 */
defineProps<{
  title: string
  description?: string
}>()
</script>

<template>
  <div class="flex flex-wrap items-start justify-between gap-x-6 gap-y-3">
    <div class="min-w-0 max-w-3xl space-y-1">
      <h2 class="text-lg font-semibold tracking-tight">
        {{ title }}
      </h2>
      <p v-if="description" class="text-sm text-muted-foreground">
        {{ description }}
      </p>
    </div>

    <slot />
  </div>
</template>
