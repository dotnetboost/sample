<script setup lang="ts">
import type { MediaFile } from '~/types'

/** One file in the grid. */
defineProps<{
  file: MediaFile
  /** Shows a primary "Attach" action instead of opening the details. */
  selectable?: boolean
  /** Already attached to the entity the picker was opened for. */
  attached?: boolean
  busy?: boolean
}>()

const emit = defineEmits<{
  open: []
  select: []
}>()
</script>

<template>
  <div
    class="group relative flex h-full flex-col overflow-hidden rounded-lg border border-default bg-default transition hover:border-inverted/20 hover:shadow-sm"
  >
    <!-- The thumbnail is pinned inside the aspect box rather than filling it. As an ordinary
         flex item its `min-height: auto` lets the image's own intrinsic height win, and a square
         image then makes its card taller than the rest of the row. -->
    <button
      type="button"
      class="relative aspect-[4/3] w-full shrink-0 cursor-pointer"
      :aria-label="`Open ${file.fileName}`"
      @click="emit('open')"
    >
      <MediaThumbnail :file="file" size="lg" class="absolute inset-0 size-full" />
    </button>

    <div class="flex min-w-0 flex-1 flex-col gap-1.5 p-3">
      <div class="flex items-start gap-2">
        <UIcon :name="iconFor(file.contentType)" class="mt-0.5 size-4 shrink-0 text-dimmed" />
        <p class="min-w-0 flex-1 truncate text-sm font-medium text-highlighted" :title="file.fileName">
          {{ file.fileName }}
        </p>
      </div>

      <div class="flex flex-wrap items-center gap-1 text-xs text-muted">
        <span>{{ formatBytes(file.size) }}</span>
        <span aria-hidden="true">·</span>
        <span :title="formatDate(file.createdAt)">{{ formatRelative(file.createdAt) }}</span>
      </div>

      <div v-if="file.category || file.tags.length" class="flex flex-wrap gap-1 pt-0.5">
        <UBadge
          v-if="file.category"
          color="neutral"
          variant="subtle"
          size="sm"
          icon="i-lucide-folder"
          :label="file.category"
        />
        <UBadge
          v-for="tag in file.tags.slice(0, 2)"
          :key="tag"
          color="neutral"
          variant="outline"
          size="sm"
          :label="tag"
        />
        <UBadge
          v-if="file.tags.length > 2"
          color="neutral"
          variant="outline"
          size="sm"
          :label="`+${file.tags.length - 2}`"
          :title="file.tags.slice(2).join(', ')"
        />
      </div>

      <div v-if="selectable" class="mt-auto pt-2">
        <UButton
          v-if="attached"
          block
          size="sm"
          color="neutral"
          variant="soft"
          icon="i-lucide-check"
          label="Attached"
          disabled
        />
        <UButton
          v-else
          block
          size="sm"
          icon="i-lucide-link"
          label="Attach"
          :loading="busy"
          @click="emit('select')"
        />
      </div>
    </div>
  </div>
</template>
