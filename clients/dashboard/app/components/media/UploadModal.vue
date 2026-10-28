<script setup lang="ts">
import { ShieldCheck, TriangleAlert, Upload, X } from '@lucide/vue'

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

/**
 * The drop zone — what `UFileUpload` drew under Nuxt UI. shadcn-vue has no file control, so it is
 * a `<label>` around a hidden file input: clicking anywhere on it opens the picker, Enter or Space
 * do the same from the keyboard because the input is still focusable, and a file dropped on it is
 * taken from the drag event.
 */
const dragging = ref(false)
const input = useTemplateRef<HTMLInputElement>('input')

function onPicked(event: Event) {
  file.value = (event.target as HTMLInputElement).files?.[0] ?? null
}

function onDrop(event: DragEvent) {
  dragging.value = false
  file.value = event.dataTransfer?.files?.[0] ?? file.value
}

function clearFile() {
  file.value = null
  if (input.value) input.value.value = ''
}

/**
 * A preview of the chosen file, before anything is sent: the picture itself for an image, so the
 * box shows what is about to be uploaded rather than only its name.
 *
 * An object URL onto the file in the browser's memory — nothing leaves the machine to draw it —
 * and released the moment it stops being shown: a new file, a cleared one, the dialog reopening or
 * closing for good. Left alone, each would hold its file's bytes until the tab closed.
 *
 * `isImage` decides, as it does for the library's own thumbnails, so an SVG gets its icon here as
 * it does everywhere else in the dashboard.
 */
const previewUrl = ref<string | null>(null)

watch(file, (next) => {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  previewUrl.value = next && isImage(next.type) ? URL.createObjectURL(next) : null
})

onBeforeUnmount(() => {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
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
  <Dialog v-model:open="open">
    <DialogContent class="sm:max-w-lg">
      <DialogHeader>
        <DialogTitle>{{ $t('media.upload.title') }}</DialogTitle>
        <DialogDescription>
          {{ owner
            ? $t('media.upload.descriptionOwner', { entity: `${owner.entityName} ${owner.entityId}` })
            : $t('media.upload.descriptionPlain') }}
        </DialogDescription>
      </DialogHeader>

      <FieldGroup class="gap-5">
        <Field>
          <FieldTitle>{{ $t('media.upload.file') }} <span class="text-destructive">*</span></FieldTitle>

          <div v-if="file" class="overflow-hidden rounded-lg border bg-muted/30">
            <!-- The picture, whole — `object-contain` on a muted ground, so a tall or a wide image
                 is shown entire rather than cropped to the box. -->
            <div v-if="previewUrl" class="flex h-48 items-center justify-center border-b bg-muted/50 p-2">
              <img
                :src="previewUrl"
                :alt="file.name"
                class="max-h-full max-w-full rounded object-contain"
              >
            </div>

            <div class="flex items-center gap-3 p-3">
              <span class="flex size-9 shrink-0 items-center justify-center rounded-md bg-background ring-1 ring-foreground/10">
                <component :is="iconFor(file.type)" class="size-4 text-muted-foreground" />
              </span>
              <div class="min-w-0 flex-1">
                <p dir="auto" class="truncate text-sm font-medium">
                  {{ file.name }}
                </p>
                <p class="text-xs text-muted-foreground">
                  {{ formatBytes(file.size) }}
                </p>
              </div>
              <Button
                variant="ghost"
                size="icon-sm"
                :aria-label="$t('common.clear')"
                @click="clearFile"
              >
                <X />
              </Button>
            </div>
          </div>

          <label
            v-else
            class="flex min-h-40 cursor-pointer flex-col items-center justify-center gap-2 rounded-lg border border-dashed p-6 text-center transition-colors focus-within:border-ring focus-within:ring-3 focus-within:ring-ring/50 hover:bg-muted/40"
            :class="{ 'border-primary bg-primary/5': dragging }"
            @dragover.prevent="dragging = true"
            @dragleave="dragging = false"
            @drop.prevent="onDrop"
          >
            <span class="flex size-10 items-center justify-center rounded-full bg-muted">
              <Upload class="size-4 text-muted-foreground" />
            </span>
            <span class="text-sm font-medium">{{ $t('media.upload.dropzone') }}</span>
            <span class="text-xs text-muted-foreground">
              {{ $t('media.upload.limit', { size: formatBytes(maxUploadBytes) }) }}
            </span>
            <input
              ref="input"
              type="file"
              class="sr-only"
              @change="onPicked"
            >
          </label>
        </Field>

        <Alert v-if="tooLarge && file" variant="destructive">
          <TriangleAlert />
          <AlertTitle>{{ $t('media.upload.tooLarge.title') }}</AlertTitle>
          <AlertDescription>
            {{ $t('media.upload.tooLarge.description', {
              fileName: file.name,
              size: formatBytes(file.size),
              limit: formatBytes(maxUploadBytes)
            }) }}
          </AlertDescription>
        </Alert>

        <Field>
          <FieldLabel for="upload-category">
            {{ $t('media.upload.category.label') }}
          </FieldLabel>
          <Input
            id="upload-category"
            v-model="category"
            dir="auto"
            :placeholder="$t('common.none')"
          />
          <FieldDescription>{{ $t('media.upload.category.description') }}</FieldDescription>
        </Field>

        <Field>
          <FieldLabel for="upload-tags">
            {{ $t('media.upload.tags.label') }}
          </FieldLabel>
          <TagsInput v-model="tags">
            <TagsInputItem v-for="tag in tags" :key="tag" :value="tag">
              <TagsInputItemText />
              <TagsInputItemDelete />
            </TagsInputItem>
            <TagsInputInput id="upload-tags" dir="auto" :placeholder="$t('media.upload.tags.placeholder')" />
          </TagsInput>
          <FieldDescription>{{ $t('media.upload.tags.description') }}</FieldDescription>
        </Field>

        <Alert>
          <ShieldCheck />
          <AlertTitle>{{ $t('media.upload.checks.title') }}</AlertTitle>
          <AlertDescription>{{ $t('media.upload.checks.description') }}</AlertDescription>
        </Alert>
      </FieldGroup>

      <DialogFooter>
        <Button variant="ghost" @click="open = false">
          {{ $t('common.cancel') }}
        </Button>
        <Button :disabled="!canUpload" @click="submit">
          <Spinner v-if="busy" />
          <Upload v-else />
          {{ $t('common.upload') }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
