<script setup lang="ts">
import { ChevronLeft, ChevronRight, Plus, Repeat2, X } from '@lucide/vue'
import type { EntityImage } from '~/types'

/**
 * One slot in a role: the image it holds, or an empty box offering to fill it.
 *
 * This is the role selector. A dropdown asks "what is this file for?" after the file has been
 * chosen, which is backwards — the page already knows what it is short of, and a box labelled
 * `primary` with nothing in it says so without being asked. Clicking it opens the library already
 * committed to that role, so there is no second answer to keep in step with the first.
 *
 * Detaching is the `x`, and it is only a detach: the file stays in the library and stays attached
 * to every other product using it. Deleting the file itself is the Media page's business.
 */
defineProps<{
  /** The file in this slot, or `null` for an empty one. */
  image?: EntityImage | null
  /** What the slot is for, used in the labels a screen reader reads out. */
  role: string
  /** Draws the box as the one that matters — the product's main image. */
  highlight?: boolean
  /** Offers to swap the file out for another, for a role that holds exactly one. */
  replaceable?: boolean
  /** Moving this one earlier or later is possible, so the arrows are worth drawing. */
  canMoveEarlier?: boolean
  canMoveLater?: boolean
  busy?: boolean
}>()

const emit = defineEmits<{
  /** The empty box, or the replace button on a full one. */
  pick: []
  detach: []
  move: [delta: -1 | 1]
}>()
</script>

<template>
  <div
    class="group relative aspect-square w-24 shrink-0 overflow-hidden rounded-lg border sm:w-28"
    :class="image
      ? [
        'bg-background',
        highlight && 'ring-2 ring-primary ring-offset-2 ring-offset-background'
      ]
      : 'border-dashed bg-muted/30 hover:border-primary hover:bg-muted/60'"
  >
    <!-- Empty: the whole box is the button, because a `+` the size of an icon in a box the size of
         a thumbnail is a small target inside a large obvious one. -->
    <button
      v-if="!image"
      type="button"
      class="flex size-full cursor-pointer flex-col items-center justify-center gap-1 text-muted-foreground transition hover:text-primary"
      :aria-label="$t('entity.roleBox.attach', { role })"
      :disabled="busy"
      @click="emit('pick')"
    >
      <Spinner v-if="busy" class="size-6" />
      <Plus v-else class="size-6" />
      <span dir="ltr" class="px-1 text-center text-[11px] leading-tight">{{ role }}</span>
    </button>

    <template v-else>
      <!-- Opens the bytes rather than the picker. The corner buttons are the two things that
           change the slot; the image itself is the one thing that cannot go wrong. -->
      <a
        :href="contentUrl(image.id)"
        target="_blank"
        rel="noopener"
        class="block size-full"
        :title="[image.fileName, formatBytes(image.size), image.altText].filter(Boolean).join(' · ')"
        :aria-label="$t('entity.roleBox.openInNewTab', { fileName: image.fileName })"
      >
        <MediaThumbnail
          :file="{
            id: image.id,
            fileName: image.fileName,
            contentType: image.contentType,
            size: image.size,
            checksum: null,
            category: null,
            tags: [],
            metadata: {},
            attachments: null,
            uploadedBy: null,
            createdAt: '',
            updatedAt: null,
            url: null
          }"
          size="md"
          class="size-full"
        />
      </a>

      <!-- Kept visible on touch, where there is no hover to reveal them. -->
      <div class="absolute inset-x-0 top-0 flex items-start justify-between p-1 opacity-100 transition sm:opacity-0 sm:group-focus-within:opacity-100 sm:group-hover:opacity-100">
        <Button
          v-if="replaceable"
          variant="secondary"
          size="icon-xs"
          class="rounded-full shadow-sm"
          :disabled="busy"
          :aria-label="$t('entity.roleBox.replace', { role })"
          @click="emit('pick')"
        >
          <Repeat2 />
        </Button>
        <span v-else />

        <Button
          size="icon-xs"
          class="rounded-full bg-destructive text-white shadow-sm hover:bg-destructive/90"
          :disabled="busy"
          :aria-label="$t('entity.roleBox.remove', { fileName: image.fileName, role })"
          @click="emit('detach')"
        >
          <Spinner v-if="busy" />
          <X v-else />
        </Button>
      </div>

      <div
        v-if="canMoveEarlier || canMoveLater"
        class="absolute inset-x-0 bottom-0 flex justify-center gap-1 bg-foreground/60 p-1 opacity-100 transition sm:opacity-0 sm:group-focus-within:opacity-100 sm:group-hover:opacity-100"
      >
        <!-- Mirrored in Arabic, where "earlier" in a row is to the right. -->
        <Button
          variant="secondary"
          size="icon-xs"
          :disabled="!canMoveEarlier || busy"
          :aria-label="$t('entity.roleBox.moveEarlier', { fileName: image.fileName })"
          @click="emit('move', -1)"
        >
          <ChevronLeft class="rtl:rotate-180" />
        </Button>
        <Button
          variant="secondary"
          size="icon-xs"
          :disabled="!canMoveLater || busy"
          :aria-label="$t('entity.roleBox.moveLater', { fileName: image.fileName })"
          @click="emit('move', 1)"
        >
          <ChevronRight class="rtl:rotate-180" />
        </Button>
      </div>
    </template>
  </div>
</template>
