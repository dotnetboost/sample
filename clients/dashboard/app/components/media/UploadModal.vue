<script setup lang="ts">
/**
 * The upload form. One file at a time, with the category and tags it should carry.
 *
 * The size is checked here against `NUXT_PUBLIC_MAX_UPLOAD_BYTES` before anything is sent — the
 * API enforces the real limit and answers a 413, but telling the user before a gigabyte has gone
 * up and come back is the difference between a form and a trap.
 */
const props = defineProps<{
  /** Uploads land attached to this entity when the picker opened the form. */
  owner?: { entityName: string, entityId: string }
  /** Prefilled from the filter bar, so uploading into the category you are browsing is one less step. */
  defaultCategory?: string
  busy?: boolean
}>()

const emit = defineEmits<{
  upload: [input: { file: File, category?: string, tags?: string[] }]
}>()

const open = defineModel<boolean>('open', { default: false })

const { maxUploadBytes } = useRuntimeConfig().public

const file = ref<File | null>(null)
const category = ref(props.defaultCategory ?? '')
const tags = ref<string[]>([])

const tooLarge = computed(() => !!file.value && file.value.size > maxUploadBytes)

const canUpload = computed(() => !!file.value && !tooLarge.value && !props.busy)

function reset() {
  file.value = null
  category.value = props.defaultCategory ?? ''
  tags.value = []
}

// Opening the form again starts from a clean one rather than the last upload's values.
watch(open, (isOpen) => {
  if (isOpen) {
    reset()
  }
})

function submit() {
  if (!file.value || tooLarge.value) {
    return
  }

  emit('upload', {
    file: file.value,
    category: category.value.trim() || undefined,
    tags: tags.value.length ? tags.value : undefined
  })
}
</script>

<template>
  <UModal
    v-model:open="open"
    title="Upload a file"
    :description="owner
      ? `Stored and attached to ${owner.entityName} ${owner.entityId} in one step.`
      : 'Stored through POST api/media. The name, size and type are recorded from the file itself.'"
  >
    <template #body>
      <div class="flex flex-col gap-4">
        <UFormField label="File" required>
          <UFileUpload
            v-model="file"
            icon="i-lucide-upload"
            label="Drop a file here, or click to choose"
            :description="`Up to ${formatBytes(maxUploadBytes)}`"
            class="w-full min-h-40"
          />
        </UFormField>

        <UAlert
          v-if="tooLarge && file"
          color="error"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          title="Too large to upload"
          :description="`${file.name} is ${formatBytes(file.size)}; the limit is ${formatBytes(maxUploadBytes)}.`"
        />

        <UFormField
          label="Category"
          description="A coarse bucket — avatars, invoices, product-images. It also leads the storage path."
        >
          <UInput v-model="category" placeholder="None" class="w-full" />
        </UFormField>

        <UFormField label="Tags" description="Up to 32, each 64 characters, no commas.">
          <UInputTags v-model="tags" placeholder="Add a tag…" class="w-full" />
        </UFormField>

        <UAlert
          color="neutral"
          variant="subtle"
          icon="i-lucide-shield-check"
          title="What the API checks"
          description="The size is measured as the bytes arrive, the type is verified against the file's own leading bytes, and executables are refused. A file that fails any of those comes back as a problem detail rather than being stored."
        />
      </div>
    </template>

    <template #footer>
      <div class="flex w-full justify-end gap-2">
        <UButton
          label="Cancel"
          color="neutral"
          variant="ghost"
          @click="open = false"
        />
        <UButton
          icon="i-lucide-upload"
          label="Upload"
          :disabled="!canUpload"
          :loading="busy"
          @click="submit"
        />
      </div>
    </template>
  </UModal>
</template>
