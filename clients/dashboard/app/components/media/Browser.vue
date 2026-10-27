<script setup lang="ts">
import { FilterX, ImageOff, PlugZap, RefreshCw, Upload } from '@lucide/vue'
import type { MediaAttachment, MediaFile } from '~/types'

/**
 * The media library: filter, sort, page, upload, inspect, delete.
 *
 * This is the whole of the Media page, and it is also what the picker on the products page shows
 * in a modal — "the same functionality" there is not a second implementation, it is this one with
 * `selectable` set. Anything added here appears in both.
 *
 * It owns its own `useMediaLibrary`, so the picker's query is independent of the page's: opening
 * the picker cannot disturb what the page behind it was showing.
 */
const props = withDefaults(defineProps<{
  /** Restricts the listing to one entity's files. */
  owner?: { entityName: string, entityId: string }
  /** Narrows that to one role. Needs `owner`; the API refuses a role without one. */
  role?: string
  /** Uploads land attached to this entity. */
  uploadOwner?: { entityName: string, entityId: string }
  /** What that entity uses an upload for. Only meaningful with `uploadOwner`. */
  uploadRole?: string
  /** Shows an "Attach" action on each file instead of opening its details. */
  selectable?: boolean
  /** Ids already attached, shown as such rather than offered again. */
  attachedIds?: string[]
  /** Fewer columns and a tighter filter bar, for the modal. */
  compact?: boolean
  pageSize?: number
}>(), {
  attachedIds: () => [],
  pageSize: 12
})

const emit = defineEmits<{
  select: [file: MediaFile]
  changed: []
}>()

const { t } = useI18n()

const owner = computed(() => props.owner)

const library = useMediaLibrary({ owner, pageSize: props.pageSize })

// A role belongs in the query rather than in a filter over the rows already loaded, for the reason
// every other filter here does: filtering one page searches one page.
watchEffect(() => {
  library.query.role = props.role
})
const {
  query, items, total, pending, loadError, busy,
  pageCount, isFiltered, categoryOptions, tagOptions,
  load, upload, update, detach, readOne, remove, reset
} = library

const uploading = ref(false)
const selected = ref<MediaFile | null>(null)
const deleting = ref<MediaFile | null>(null)

/** The selected file, re-read from the current page so the slideover follows a save. */
const selectedFile = computed(() => selected.value
  ? items.value.find(file => file.id === selected.value!.id) ?? selected.value
  : null)

/**
 * Who is using the selected file. A listing does not carry the links — a page of files would need a
 * query each — so opening one file reads it on its own. Re-read after every change, because
 * attaching and detaching are exactly what this shows.
 */
const attachments = ref<MediaAttachment[]>([])
const loadingAttachments = ref(false)

async function loadAttachments(id: string | undefined) {
  if (!id) {
    attachments.value = []
    return
  }

  loadingAttachments.value = true

  try {
    attachments.value = (await readOne(id)).attachments ?? []
  } catch {
    // Not worth an error banner: the panel says "used by" nothing rather than lying about a count,
    // and every action on it reports its own failure.
    attachments.value = []
  } finally {
    loadingAttachments.value = false
  }
}

watch(() => selected.value?.id, id => loadAttachments(id), { immediate: true })

/** The file being deleted, with its links, so the confirmation can say what else loses it. */
const deletingUsedBy = ref<MediaAttachment[]>([])

async function confirmDelete(file: MediaFile) {
  // The links this file already has loaded, before closing the panel that holds them.
  const known = selected.value?.id === file.id ? attachments.value : null

  // Close the details first. The confirmation is what carries the warning about every entity losing
  // the file, and it opens underneath the slideover's overlay — dimmed, and impossible to read or
  // click — if the panel that raised it is still open.
  selected.value = null

  deleting.value = file
  deletingUsedBy.value = known
    ?? await readOne(file.id).then(read => read.attachments ?? []).catch(() => [])
}

/**
 * What deleting this file takes with it. Deleting is not detaching: the bytes go, and every entity
 * using the file loses it at once. Saying how many is the difference between an informed click and
 * a surprise.
 */
const deleteWarning = computed(() => {
  const entities = new Set(deletingUsedBy.value.map(a => `${a.ownerEntityName}/${a.ownerEntityId}`))

  // Three messages rather than one with a count, because none/one/many are three different
  // sentences here and not three forms of one — the last names the entities and the others cannot.
  if (entities.size === 0) {
    return t('media.browser.confirmDelete.usedByNone')
  }

  if (entities.size === 1) {
    return t('media.browser.confirmDelete.usedByOne', {
      entity: [...entities][0]!.replace('/', ' ')
    })
  }

  return t('media.browser.confirmDelete.usedByMany', {
    count: entities.size,
    entities: [...entities].map(key => key.replace('/', ' ')).join(', ')
  })
})

const attached = (file: MediaFile) => props.attachedIds.includes(file.id)

/**
 * The card grid, shared by the skeletons and the cards they stand in for — two copies of the
 * breakpoints is two chances for the placeholders to be laid out unlike the thing they replace.
 * Compact only drops the widest column; the narrower ones are the same in the modal and the page.
 */
const gridClass = computed(() => [
  'grid gap-4 sm:grid-cols-2 lg:grid-cols-3',
  props.compact ? '' : 'xl:grid-cols-4'
])

const range = computed(() => {
  if (!total.value) {
    return ''
  }

  return t('media.browser.range', {
    from: (query.page - 1) * query.pageSize + 1,
    to: Math.min(query.page * query.pageSize, total.value),
    total: total.value
  })
})

async function onUpload(input: { file: File, category?: string, tags?: string[] }) {
  const result = await upload({ ...input, owner: props.uploadOwner, role: props.uploadRole })

  if (result) {
    uploading.value = false
    emit('changed')
  }
}

async function onSave(patch: { fileName: string, category: string, tags: string[] }) {
  if (!selectedFile.value) {
    return
  }

  await update(selectedFile.value.id, {
    fileName: patch.fileName,
    // An emptied box means "no category", and the API needs an empty string to say so.
    category: patch.category,
    tags: patch.tags
  })

  emit('changed')
}

async function onDetach(attachment: MediaAttachment) {
  if (!selectedFile.value) {
    return
  }

  // One link per click. The panel lists a file's roles separately, so the id the button carries is
  // exactly the one the user pointed at.
  await detach(selectedFile.value.id, attachment.id)

  await loadAttachments(selectedFile.value.id)
  emit('changed')
}

async function onDelete() {
  const file = deleting.value
  if (!file) {
    return
  }

  await remove(file)

  deleting.value = null
  deletingUsedBy.value = []
  if (selected.value?.id === file.id) {
    selected.value = null
  }

  emit('changed')
}

function onSelect(file: MediaFile) {
  emit('select', file)
}

// The picker opens the browser inside a modal that is mounted once and reused, so the parent
// needs a way to reload it when something outside changed.
defineExpose({ reload: load })

onMounted(load)
</script>

<template>
  <div class="flex flex-col gap-4">
    <div class="flex flex-col gap-2 lg:flex-row lg:items-center">
      <MediaFilters
        :query="query"
        :category-options="categoryOptions"
        :tag-options="tagOptions"
        :is-filtered="isFiltered"
        :pending="pending"
        :compact="compact"
        class="flex-1"
        @update="Object.assign(query, $event)"
        @reset="reset"
      />

      <div class="flex items-center gap-2 lg:shrink-0">
        <Button
          variant="ghost"
          size="icon"
          :disabled="pending"
          :aria-label="$t('common.refresh')"
          @click="load"
        >
          <RefreshCw :class="{ 'animate-spin': pending }" />
        </Button>
        <Button class="flex-1 lg:flex-none" @click="uploading = true">
          <Upload />
          {{ $t('common.upload') }}
        </Button>
      </div>
    </div>

    <Alert v-if="loadError" variant="destructive">
      <PlugZap />
      <AlertTitle>{{ $t('media.browser.loadError') }}</AlertTitle>
      <AlertDescription>{{ loadError }}</AlertDescription>
      <AlertAction>
        <Button variant="outline" size="sm" @click="load">
          {{ $t('common.tryAgain') }}
        </Button>
      </AlertAction>
    </Alert>

    <template v-else>
      <div class="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
        <Badge variant="outline" dir="ltr">
          GET api/media
        </Badge>
        <span v-if="range">{{ range }}</span>
        <span v-if="isFiltered" class="text-muted-foreground/70">· {{ $t('common.filtered') }}</span>
      </div>

      <div v-if="pending && !items.length" :class="gridClass">
        <Skeleton v-for="n in pageSize" :key="n" class="h-56 rounded-xl" />
      </div>

      <Empty v-else-if="!items.length" class="border border-dashed">
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <ImageOff />
          </EmptyMedia>
          <EmptyTitle>
            {{ isFiltered
              ? $t('media.browser.emptyFiltered.title')
              : $t('media.browser.emptyNone.title') }}
          </EmptyTitle>
          <EmptyDescription>
            {{ isFiltered
              ? $t('media.browser.emptyFiltered.description')
              : $t('media.browser.emptyNone.description') }}
          </EmptyDescription>
        </EmptyHeader>
        <EmptyContent>
          <Button
            v-if="isFiltered"
            variant="outline"
            size="sm"
            @click="reset"
          >
            <FilterX />
            {{ $t('media.browser.clearFilters') }}
          </Button>
          <Button v-else size="sm" @click="uploading = true">
            <Upload />
            {{ $t('media.browser.uploadFirst') }}
          </Button>
        </EmptyContent>
      </Empty>

      <div v-else :class="gridClass">
        <MediaCard
          v-for="file in items"
          :key="file.id"
          :file="file"
          :selectable="selectable"
          :attached="attached(file)"
          :busy="busy === file.id"
          @open="selected = file"
          @select="onSelect(file)"
        />
      </div>

      <div v-if="pageCount > 1" class="flex justify-center pt-1">
        <PagerControls
          v-model:page="query.page"
          :total="total"
          :items-per-page="query.pageSize"
        />
      </div>
    </template>

    <MediaUploadModal
      v-model:open="uploading"
      :owner="uploadOwner"
      :default-category="query.category || undefined"
      :busy="pending"
      @upload="onUpload"
    />

    <MediaDetailSlideover
      v-if="selectedFile"
      :open="selected !== null"
      :file="selectedFile"
      :attachments="attachments"
      :loading-attachments="loadingAttachments"
      :busy="busy === selectedFile.id"
      @update:open="!$event && (selected = null)"
      @save="onSave"
      @detach="onDetach"
      @remove="confirmDelete(selectedFile)"
    />

    <ConfirmModal
      :open="deleting !== null"
      :title="$t('media.browser.confirmDelete.title', { fileName: deleting?.fileName ?? '' })"
      :description="$t('media.browser.confirmDelete.description', { usage: deleteWarning })"
      :confirm-label="$t('common.delete')"
      :loading="busy === deleting?.id"
      @update:open="!$event && (deleting = null)"
      @confirm="onDelete"
    />
  </div>
</template>
