<script setup lang="ts">
import { Download, ExternalLink, HardDrive, Link, Link2, Save, Trash2, Unlink } from '@lucide/vue'
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
const { direction } = useShellLocale()

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
  <!--
    Not `EditorSheet`: its title is a plain string, and this one needs a `dir` of its own — a file
    called `321106641.png` reads as `png.321106641` in the RTL panel header without one.
  -->
  <Sheet v-model:open="open">
    <SheetContent :side="direction === 'rtl' ? 'left' : 'right'" class="w-full gap-0 p-0 sm:max-w-lg">
      <SheetHeader class="border-b p-6">
        <SheetTitle class="truncate pe-8">
          <span dir="auto">{{ file.fileName }}</span>
        </SheetTitle>
        <SheetDescription>{{ $t('media.detail.description') }}</SheetDescription>
      </SheetHeader>

      <div class="min-h-0 flex-1 overflow-y-auto p-6">
        <FieldGroup class="gap-5">
          <div class="relative aspect-[16/9] w-full overflow-hidden rounded-lg border">
            <MediaThumbnail :file="file" size="lg" class="absolute inset-0 size-full" />
          </div>

          <div class="flex flex-wrap gap-2">
            <Button as-child variant="outline" size="sm">
              <a :href="contentUrl(file.id)" target="_blank" rel="noopener">
                <ExternalLink />
                {{ $t('common.open') }}
              </a>
            </Button>
            <Button as-child variant="outline" size="sm">
              <a :href="contentUrl(file.id, true)">
                <Download />
                {{ $t('common.download') }}
              </a>
            </Button>
            <Button
              v-if="file.url"
              as-child
              variant="outline"
              size="sm"
            >
              <a
                :href="file.url"
                target="_blank"
                rel="noopener"
                :title="$t('media.detail.directLinkTitle')"
              >
                <Link2 />
                {{ $t('media.detail.directLink') }}
              </a>
            </Button>
          </div>

          <Field>
            <FieldLabel for="media-file-name">
              {{ $t('media.detail.fileName.label') }}
            </FieldLabel>
            <Input id="media-file-name" v-model="fileName" dir="auto" />
            <FieldDescription>{{ $t('media.detail.fileName.description') }}</FieldDescription>
          </Field>

          <Field>
            <FieldLabel for="media-category">
              {{ $t('media.detail.category.label') }}
            </FieldLabel>
            <Input
              id="media-category"
              v-model="category"
              dir="auto"
              :placeholder="$t('common.none')"
            />
            <FieldDescription>{{ $t('media.detail.category.description') }}</FieldDescription>
          </Field>

          <Field>
            <FieldLabel for="media-tags">
              {{ $t('media.detail.tags.label') }}
            </FieldLabel>
            <TagsInput v-model="tags">
              <TagsInputItem v-for="tag in tags" :key="tag" :value="tag">
                <TagsInputItemText />
                <TagsInputItemDelete />
              </TagsInputItem>
              <TagsInputInput id="media-tags" dir="auto" :placeholder="$t('media.detail.tags.placeholder')" />
            </TagsInput>
            <FieldDescription>{{ $t('media.detail.tags.description') }}</FieldDescription>
          </Field>

          <Field>
            <FieldTitle>{{ $t('media.detail.usedBy.label') }}</FieldTitle>
            <FieldDescription>
              {{ usedBy.length > 1
                ? $t('media.detail.usedBy.descriptionMany')
                : $t('media.detail.usedBy.descriptionOne') }}
            </FieldDescription>

            <Skeleton v-if="loadingAttachments" class="h-7 w-40" />

            <p v-else-if="!usedBy.length" class="text-sm text-muted-foreground">
              {{ $t('media.detail.usedBy.none') }}
            </p>

            <div v-else class="flex flex-col gap-1.5">
              <div
                v-for="entity in usedBy"
                :key="`${entity.entityName}/${entity.entityId}`"
                class="flex flex-wrap items-center gap-1.5"
              >
                <!-- `dir="ltr"`: an owner and a role are identifiers the API chose, never prose. -->
                <Badge class="bg-primary/10 text-primary" dir="ltr">
                  <Link />
                  {{ entity.entityName }} {{ entity.entityId }}
                </Badge>

                <!-- One chip per link, each removable on its own: an entity can hold the same file
                     under several roles, and a single button for the group could only ever detach
                     one of them. -->
                <Button
                  v-for="link in entity.links"
                  :key="link.id"
                  variant="outline"
                  size="xs"
                  dir="ltr"
                  :disabled="busy"
                  :aria-label="$t('media.detail.usedBy.detach', {
                    role: link.role,
                    entity: `${entity.entityName} ${entity.entityId}`
                  })"
                  @click="emit('detach', link)"
                >
                  <Unlink />
                  {{ link.role }}
                </Button>
              </div>
            </div>
          </Field>

          <FieldSeparator />

          <!-- The sample's one addition to this panel: per-language alt text, which lives in
               DotNetBoost.MultiLingual rather than in the media row. -->
          <MediaAltTextSection :media-file-id="file.id" />

          <FieldSeparator />

          <dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
            <template v-for="fact in facts" :key="fact.label">
              <dt class="text-muted-foreground">
                {{ fact.label }}
              </dt>
              <!-- A checksum, a GUID and a media type are identifiers: always left-to-right. -->
              <dd
                :dir="fact.mono ? 'ltr' : undefined"
                :class="[fact.mono && 'font-mono text-xs', fact.truncate && 'truncate']"
                :title="fact.truncate ? fact.value : undefined"
              >
                {{ fact.value }}
              </dd>
            </template>
          </dl>

          <Alert>
            <HardDrive />
            <AlertTitle>{{ $t('media.detail.storage.title') }}</AlertTitle>
            <AlertDescription>{{ $t('media.detail.storage.description') }}</AlertDescription>
          </Alert>
        </FieldGroup>
      </div>

      <SheetFooter class="flex-row items-center border-t p-4">
        <Button variant="destructive" :disabled="busy" @click="emit('remove')">
          <Trash2 />
          {{ $t('common.delete') }}
        </Button>
        <Button variant="ghost" class="ms-auto" @click="open = false">
          {{ $t('common.close') }}
        </Button>
        <Button :disabled="!dirty || busy" @click="save">
          <Spinner v-if="busy" />
          <Save v-else />
          {{ $t('common.save') }}
        </Button>
      </SheetFooter>
    </SheetContent>
  </Sheet>
</template>
