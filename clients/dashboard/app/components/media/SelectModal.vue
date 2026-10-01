<script setup lang="ts">
import type { MediaFile } from '~/types'

/**
 * The media library, for picking a file that is NOT attached to anything.
 *
 * The branding logo belongs to the application rather than to a row, so there is no owner to
 * attach it to and no role to choose: the setting simply stores the file's id. That is the whole
 * of the Settings × Media composition, and it needs no attachment.
 *
 * Still the same `MediaBrowser` the Media page is — searching, filtering, sorting, paging and
 * uploading all behave identically, because there is no second implementation.
 */
defineProps<{
  title?: string
}>()

const emit = defineEmits<{ select: [file: MediaFile] }>()

const open = defineModel<boolean>('open', { default: false })

function choose(file: MediaFile) {
  emit('select', file)
  open.value = false
}
</script>

<template>
  <UModal
    v-model:open="open"
    :title="title ?? 'Choose a file'"
    description="Every file in DotNetBoost.Media. Only its id is stored in the setting."
    :ui="{ content: 'max-w-4xl' }"
  >
    <template #body>
      <MediaBrowser
        selectable
        compact
        select-label="Choose"
        :page-size="8"
        @select="choose"
      />
    </template>
  </UModal>
</template>
