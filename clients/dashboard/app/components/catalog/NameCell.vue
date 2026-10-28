<script setup lang="ts">
import { ImageOff } from '@lucide/vue'
import type { MediaRef } from '~/types'

/**
 * A catalogue row's first cell: its image, and its name over its description.
 *
 * The name is a real button, so the row's editor is reachable from the keyboard; it stops the
 * click, because the row behind it opens the same editor. `dir="auto"` on both texts: a row with
 * no Arabic translation comes back in English, and read right-to-left it truncates from the wrong
 * end.
 */
defineProps<{
  image: MediaRef | null
  name: string
  description: string
  /** How wide the text may grow, per breakpoint. */
  widthClass: string
}>()

const emit = defineEmits<{
  open: []
}>()
</script>

<template>
  <div class="flex min-w-0 items-center gap-3">
    <div class="flex size-10 shrink-0 items-center justify-center overflow-hidden rounded-md border bg-muted/40">
      <img
        v-if="image"
        :src="contentUrl(image.id)"
        :alt="image.altText ?? name"
        class="size-full object-cover"
      >
      <ImageOff v-else class="size-4 text-muted-foreground" />
    </div>
    <div class="min-w-0" :class="widthClass">
      <button
        type="button"
        dir="auto"
        class="block max-w-full truncate text-start font-medium outline-none hover:underline focus-visible:underline"
        @click.stop="emit('open')"
      >
        {{ name }}
      </button>
      <p dir="auto" class="truncate text-muted-foreground">
        {{ description }}
      </p>
    </div>
  </div>
</template>
