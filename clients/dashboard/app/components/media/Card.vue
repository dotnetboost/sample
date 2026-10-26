<script setup lang="ts">
import { Check, Folder, Link } from '@lucide/vue'
// Imported by hand, unlike every other shadcn-vue component in this dashboard: inside a file
// called Card.vue, a bare `<Card>` is Vue's implicit self-reference, and the card would render
// itself forever.
import { Card } from '~/components/ui/card'
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
  <Card
    class="group relative h-full gap-0 py-0 transition hover:shadow-md hover:ring-foreground/20"
  >
    <!-- The thumbnail is pinned inside the aspect box rather than filling it. As an ordinary
         flex item its `min-height: auto` lets the image's own intrinsic height win, and a square
         image then makes its card taller than the rest of the row. -->
    <button
      type="button"
      class="relative aspect-[4/3] w-full shrink-0 cursor-pointer overflow-hidden"
      :aria-label="$t('media.card.openFile', { fileName: file.fileName })"
      @click="emit('open')"
    >
      <MediaThumbnail
        :file="file"
        size="lg"
        class="absolute inset-0 size-full transition-transform duration-300 group-hover:scale-[1.03]"
      />
    </button>

    <div class="flex min-w-0 flex-1 flex-col gap-1.5 border-t p-3">
      <div class="flex items-start gap-2">
        <component :is="iconFor(file.contentType)" class="mt-0.5 size-4 shrink-0 text-muted-foreground" />
        <!-- `dir="auto"`: an uploaded name is the uploader's, not ours. See GroupForm. -->
        <p
          dir="auto"
          class="min-w-0 flex-1 truncate font-medium"
          :title="file.fileName"
        >
          {{ file.fileName }}
        </p>
      </div>

      <div class="flex flex-wrap items-center gap-1 text-xs text-muted-foreground">
        <span>{{ formatBytes(file.size) }}</span>
        <span aria-hidden="true">·</span>
        <span :title="formatDate(file.createdAt)">{{ formatRelative(file.createdAt) }}</span>
      </div>

      <div v-if="file.category || file.tags.length" class="flex flex-wrap gap-1 pt-0.5">
        <Badge v-if="file.category" variant="secondary">
          <Folder />
          {{ file.category }}
        </Badge>
        <Badge v-for="tag in file.tags.slice(0, 2)" :key="tag" variant="outline">
          {{ tag }}
        </Badge>
        <Badge
          v-if="file.tags.length > 2"
          variant="outline"
          :title="file.tags.slice(2).join(', ')"
        >
          +{{ file.tags.length - 2 }}
        </Badge>
      </div>

      <div v-if="selectable" class="mt-auto pt-2">
        <Button
          v-if="attached"
          size="sm"
          variant="secondary"
          class="w-full"
          disabled
        >
          <Check />
          {{ $t('media.card.attached') }}
        </Button>
        <Button
          v-else
          size="sm"
          class="w-full"
          :disabled="busy"
          @click="emit('select')"
        >
          <Spinner v-if="busy" />
          <Link v-else />
          {{ $t('media.card.attach') }}
        </Button>
      </div>
    </div>
  </Card>
</template>
