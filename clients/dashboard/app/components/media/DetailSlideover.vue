<script setup lang="ts">
import type { MediaAttachment, MediaFile } from '~/types'

/**
 * One file in full: a preview, everything the row records, and the fields that can be changed.
 *
 * The bytes, their size, their checksum and where they live are shown but not editable — the API
 * refuses to change them, because replacing content means a new upload.
 */
const props = defineProps<{
  file: MediaFile
  /**
   * Everything using this file. Passed in rather than read from `file.attachments`, because the row
   * in a listing does not carry them — the browser reads the single file for this.
   */
  attachments?: MediaAttachment[]
  /** While the attachments are being read, so the panel does not claim "used by nothing" too early. */
  loadingAttachments?: boolean
  busy?: boolean
}>()

const emit = defineEmits<{
  save: [patch: { fileName: string, category: string, tags: string[] }]
  detach: [attachment: MediaAttachment]
  remove: []
}>()

const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()

const fileName = ref(props.file.fileName)
const category = ref(props.file.category ?? '')
const tags = ref<string[]>([...props.file.tags])

// The slideover is reused for whichever file is selected, so its fields follow the selection.
watch(() => props.file, (file) => {
  fileName.value = file.fileName
  category.value = file.category ?? ''
  tags.value = [...file.tags]
}, { deep: true })

/**
 * The links, grouped by the entity holding them, so a file used twice by one product reads as one
 * product with two roles rather than as two unrelated rows.
 */
const usedBy = computed(() => {
  const groups = new Map<string, { entityName: string, entityId: string, links: MediaAttachment[] }>()

  for (const attachment of props.attachments ?? []) {
    const key = `${attachment.ownerEntityName}/${attachment.ownerEntityId}`
    const group = groups.get(key)
      ?? { entityName: attachment.ownerEntityName, entityId: attachment.ownerEntityId, links: [] }

    group.links.push(attachment)
    groups.set(key, group)
  }

  return [...groups.values()]
})

const dirty = computed(() =>
  fileName.value.trim() !== props.file.fileName
  || category.value.trim() !== (props.file.category ?? '')
  || tags.value.join(',') !== props.file.tags.join(','))

function save() {
  emit('save', {
    fileName: fileName.value.trim(),
    category: category.value.trim(),
    tags: tags.value
  })
}

/** Everything the row records that is not editable, in the order it is worth reading. */
const facts = computed(() => [
  { label: t('media.detail.facts.type'), value: props.file.contentType, mono: true },
  { label: t('media.detail.facts.size'), value: formatBytes(props.file.size) },
  { label: t('media.detail.facts.uploaded'), value: formatDate(props.file.createdAt) },
  {
    label: t('media.detail.facts.updated'),
    value: props.file.updatedAt ? formatDate(props.file.updatedAt) : t('common.empty')
  },
  { label: t('media.detail.facts.uploadedBy'), value: props.file.uploadedBy ?? t('common.empty') },
  {
    label: t('media.detail.facts.checksum'),
    value: props.file.checksum ?? t('common.empty'),
    mono: true,
    truncate: true
  },
  { label: t('media.detail.facts.id'), value: props.file.id, mono: true, truncate: true }
])
</script>

<template>
  <USlideover
    v-model:open="open"
    :title="file.fileName"
    :description="$t('media.detail.description')"
    :ui="{ content: 'sm:max-w-lg' }"
  >
    <!-- Through the slot, not the `title` prop: the prop has nowhere to carry a `dir`, and a
         file called `321106641.png` reads as `png.321106641` in the RTL panel header without one. -->
    <template #title>
      <span dir="auto">{{ file.fileName }}</span>
    </template>

    <template #body>
      <div class="flex flex-col gap-5">
        <div class="relative aspect-[16/9] w-full overflow-hidden rounded-lg border border-default">
          <MediaThumbnail :file="file" size="lg" class="absolute inset-0 size-full" />
        </div>

        <div class="flex flex-wrap gap-2">
          <UButton
            icon="i-lucide-external-link"
            color="neutral"
            variant="outline"
            size="sm"
            :label="$t('common.open')"
            :to="contentUrl(file.id)"
            target="_blank"
          />
          <UButton
            icon="i-lucide-download"
            color="neutral"
            variant="outline"
            size="sm"
            :label="$t('common.download')"
            :to="contentUrl(file.id, true)"
          />
          <UButton
            v-if="file.url"
            icon="i-lucide-link-2"
            color="neutral"
            variant="outline"
            size="sm"
            :label="$t('media.detail.directLink')"
            :to="file.url"
            target="_blank"
            :title="$t('media.detail.directLinkTitle')"
          />
        </div>

        <UFormField
          :label="$t('media.detail.fileName.label')"
          :description="$t('media.detail.fileName.description')"
        >
          <UInput v-model="fileName" dir="auto" class="w-full" />
        </UFormField>

        <UFormField
          :label="$t('media.detail.category.label')"
          :description="$t('media.detail.category.description')"
        >
          <UInput
            v-model="category"
            dir="auto"
            :placeholder="$t('common.none')"
            class="w-full"
          />
        </UFormField>

        <UFormField
          :label="$t('media.detail.tags.label')"
          :description="$t('media.detail.tags.description')"
        >
          <UInputTags
            v-model="tags"
            dir="auto"
            :placeholder="$t('media.detail.tags.placeholder')"
            class="w-full"
          />
        </UFormField>

        <UFormField
          :label="$t('media.detail.usedBy.label')"
          :description="usedBy.length > 1
            ? $t('media.detail.usedBy.descriptionMany')
            : $t('media.detail.usedBy.descriptionOne')"
        >
          <USkeleton v-if="loadingAttachments" class="h-7 w-40" />

          <p v-else-if="!usedBy.length" class="text-sm text-muted">
            {{ $t('media.detail.usedBy.none') }}
          </p>

          <div v-else class="flex flex-col gap-1.5">
            <div
              v-for="entity in usedBy"
              :key="`${entity.entityName}/${entity.entityId}`"
              class="flex flex-wrap items-center gap-1.5"
            >
              <!-- `dir="ltr"`: an owner and a role are identifiers the API chose, never prose. -->
              <UBadge
                color="primary"
                variant="subtle"
                dir="ltr"
                icon="i-lucide-link"
                :label="`${entity.entityName} ${entity.entityId}`"
              />

              <!-- One chip per link, each removable on its own: an entity can hold the same file
                   under several roles, and a single button for the group could only ever detach
                   one of them. -->
              <UButton
                v-for="link in entity.links"
                :key="link.id"
                color="neutral"
                variant="outline"
                size="xs"
                dir="ltr"
                icon="i-lucide-unlink"
                :label="link.role"
                :aria-label="$t('media.detail.usedBy.detach', {
                  role: link.role,
                  entity: `${entity.entityName} ${entity.entityId}`
                })"
                :loading="busy"
                @click="emit('detach', link)"
              />
            </div>
          </div>
        </UFormField>

        <USeparator />

        <!-- The sample's one addition to this panel: per-language alt text, which lives in
             DotNetBoost.MultiLingual rather than in the media row. -->
        <MediaAltTextSection :media-file-id="file.id" />

        <USeparator />

        <dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
          <template v-for="fact in facts" :key="fact.label">
            <dt class="text-muted">
              {{ fact.label }}
            </dt>
            <!-- A checksum, a GUID and a media type are identifiers: always left-to-right. -->
            <dd
              :dir="fact.mono ? 'ltr' : undefined"
              class="text-highlighted"
              :class="[fact.mono && 'font-mono text-xs', fact.truncate && 'truncate']"
              :title="fact.truncate ? fact.value : undefined"
            >
              {{ fact.value }}
            </dd>
          </template>
        </dl>

        <UAlert
          color="neutral"
          variant="subtle"
          icon="i-lucide-hard-drive"
          :title="$t('media.detail.storage.title')"
          :description="$t('media.detail.storage.description')"
        />
      </div>
    </template>

    <template #footer>
      <div class="flex w-full items-center gap-2">
        <UButton
          icon="i-lucide-trash-2"
          color="error"
          variant="ghost"
          :label="$t('common.delete')"
          :loading="busy"
          @click="emit('remove')"
        />
        <UButton
          class="ms-auto"
          color="neutral"
          variant="ghost"
          :label="$t('common.close')"
          @click="open = false"
        />
        <UButton
          icon="i-lucide-save"
          :label="$t('common.save')"
          :disabled="!dirty"
          :loading="busy"
          @click="save"
        />
      </div>
    </template>
  </USlideover>
</template>
