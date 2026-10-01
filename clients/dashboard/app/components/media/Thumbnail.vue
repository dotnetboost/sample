<script setup lang="ts">
import type { MediaFile } from '~/types'

/**
 * A file's preview: the image itself when it is one, otherwise an icon for its type.
 *
 * The image is fetched through `/api/media/{id}/content`, this app's own proxied route, rather
 * than `file.url`. A backend URL is signed and expires, and would 403 in a long-open tab; the
 * proxied route is stable and goes through whatever authorization the API has in front of it.
 */
const props = defineProps<{
  file: MediaFile
  size?: 'sm' | 'md' | 'lg'
}>()

const failed = ref(false)

const showImage = computed(() => isImage(props.file.contentType) && !failed.value)

const iconSize = computed(() => ({ sm: 'size-5', md: 'size-8', lg: 'size-10' }[props.size ?? 'md']))

// The id changes when the tile is reused for another file; a previous failure must not stick.
watch(() => props.file.id, () => failed.value = false)
</script>

<template>
  <div class="flex items-center justify-center overflow-hidden bg-elevated/50">
    <img
      v-if="showImage"
      :src="contentUrl(file.id)"
      :alt="file.fileName"
      loading="lazy"
      decoding="async"
      class="size-full object-cover"
      @error="failed = true"
    >
    <UIcon
      v-else
      :name="iconFor(file.contentType)"
      :class="[iconSize, 'text-dimmed']"
    />
  </div>
</template>
