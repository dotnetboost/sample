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
    :title="$t('media.upload.title')"
    :description="owner
      ? $t('media.upload.descriptionOwner', { entity: `${owner.entityName} ${owner.entityId}` })
      : $t('media.upload.descriptionPlain')"
  >
    <template #body>
      <div class="flex flex-col gap-4">
        <UFormField :label="$t('media.upload.file')" required>
          <UFileUpload
            v-model="file"
            icon="i-lucide-upload"
            :label="$t('media.upload.dropzone')"
            :description="$t('media.upload.limit', { size: formatBytes(maxUploadBytes) })"
            class="w-full min-h-40"
          />
        </UFormField>

        <UAlert
          v-if="tooLarge && file"
          color="error"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          :title="$t('media.upload.tooLarge.title')"
          :description="$t('media.upload.tooLarge.description', {
            fileName: file.name,
            size: formatBytes(file.size),
            limit: formatBytes(maxUploadBytes)
          })"
        />

        <UFormField
          :label="$t('media.upload.category.label')"
          :description="$t('media.upload.category.description')"
        >
          <UInput
            v-model="category"
            dir="auto"
            :placeholder="$t('common.none')"
            class="w-full"
          />
        </UFormField>

        <UFormField
          :label="$t('media.upload.tags.label')"
          :description="$t('media.upload.tags.description')"
        >
          <UInputTags
            v-model="tags"
            dir="auto"
            :placeholder="$t('media.upload.tags.placeholder')"
            class="w-full"
          />
        </UFormField>

        <UAlert
          color="neutral"
          variant="subtle"
          icon="i-lucide-shield-check"
          :title="$t('media.upload.checks.title')"
          :description="$t('media.upload.checks.description')"
        />
      </div>
    </template>

    <template #footer>
      <div class="flex w-full justify-end gap-2">
        <UButton
          :label="$t('common.cancel')"
          color="neutral"
          variant="ghost"
          @click="open = false"
        />
        <UButton
          icon="i-lucide-upload"
          :label="$t('common.upload')"
          :disabled="!canUpload"
          :loading="busy"
          @click="submit"
        />
      </div>
    </template>
  </UModal>
</template>
