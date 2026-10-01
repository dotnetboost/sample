<script setup lang="ts">
import type { EntityImage, MediaFile, MediaRole } from '~/types'

/**
 * One entity's files, a box per role — lifted wholesale from the DotNetBoost.Media dashboard's
 * product page, because an entity's attachments look the same whatever the entity is.
 *
 * The roles are read from the media library (`GET /api/media/roles/{entityName}`), not written
 * down here: `Program.cs` declares them with `DeclareRoles(...)` at startup, so adding a role to
 * an entity grows a box with no change to this component. A single-valued role gets exactly one
 * box, filled or empty; a multi-valued one gets a box per file plus a trailing empty one.
 *
 * Nothing here is a draft. An attach, a detach or a reorder is a request of its own, landed before
 * the boxes redraw — because they are rows in the media library, not fields on the entity being
 * edited, and the entity's own Save cannot touch them.
 */
const props = defineProps<{
  entityName: string
  entityId: string
  images: EntityImage[]
}>()

const emit = defineEmits<{ changed: [] }>()

const toast = useToast()
const { attach, detach, reorder } = useAttachments()

const owner = computed(() => ({ entityName: props.entityName, entityId: props.entityId }))

const declared = ref<MediaRole[]>([])
const busy = ref<string | null>(null)
const picking = ref<string | null>(null)
const mediaVersion = ref(0)

async function loadRoles() {
  try {
    declared.value = await $fetch<MediaRole[]>(`/api/media/roles/${encodeURIComponent(props.entityName)}`)
  } catch {
    // Not worth blocking on: the fallback below still draws every attached file.
    declared.value = []
  }
}

watch(() => props.entityName, loadRoles, { immediate: true })

/**
 * One row per role: the images in it, and whether it can hold more than one.
 */
const rows = computed(() => {
  const held = (role: string) => props.images
    .filter(image => image.role === role)
    .sort((a, b) => a.sortOrder - b.sortOrder)

  const names = declared.value.length
    ? declared.value.map(role => role.name)
    // Nothing declared: show what is attached, which is all this component can honestly know.
    : [...new Set(props.images.map(image => image.role))].sort()

  return names.map(name => ({
    name,
    multiple: declared.value.find(r => r.name === name)?.multiple ?? true,
    images: held(name)
  }))
})

async function act(key: string | null, call: () => Promise<unknown>, success: string, failure: string) {
  busy.value = key

  try {
    await call()
    toast.add({ title: success, icon: 'i-lucide-check', color: 'success' })
    mediaVersion.value++
    emit('changed')
  } catch (error) {
    toast.add({
      title: failure,
      description: problemMessage(error),
      icon: 'i-lucide-triangle-alert',
      color: 'error'
    })
  } finally {
    busy.value = null
  }
}

/**
 * `replace` is what makes a single-valued role single-valued: the library takes the role over from
 * whatever held it, and the displaced file is only detached — it stays in the library and stays
 * attached to every other entity using it.
 */
function onAttach(file: MediaFile, role: string, replace: boolean) {
  return act(
    file.id,
    () => attach(file.id, owner.value, role, replace),
    `${file.fileName} is now this ${props.entityName.toLowerCase()}'s ${role}`,
    'Could not attach the file'
  )
}

/**
 * Detaching needs the ATTACHMENT's id, not the file's — a file may be attached several times — so
 * the single file is read first to find the link that points here.
 */
function onDetach(image: EntityImage) {
  return act(
    image.id,
    async () => {
      const file = await $fetch<MediaFile>(`/api/media/${image.id}`)

      const link = file.attachments?.find(a =>
        a.ownerEntityName === props.entityName
        && a.ownerEntityId === props.entityId
        && a.role === image.role)

      if (!link) throw new Error('That attachment no longer exists.')

      return detach(image.id, link.id)
    },
    `Removed ${image.fileName} from ${image.role}`,
    'Could not remove the image'
  )
}

/** Moves one image one place within its role, and saves the whole order. */
function onMove(role: string, image: EntityImage, delta: -1 | 1) {
  const held = rows.value.find(row => row.name === role)?.images ?? []

  const from = held.findIndex(i => i.id === image.id)
  const to = from + delta

  if (from < 0 || to < 0 || to >= held.length) return

  const ids = held.map(i => i.id)
  ;[ids[from], ids[to]] = [ids[to]!, ids[from]!]

  return act(
    image.id,
    // Every id in the role: the ones left out would have no defined position, and the API
    // refuses a partial order.
    () => reorder(owner.value, role, ids),
    `${role} order saved`,
    'Could not save the order'
  )
}
</script>

<template>
  <div class="space-y-4">
    <div v-for="row in rows" :key="row.name" class="space-y-2">
      <div class="flex items-center gap-2">
        <p class="text-sm font-medium capitalize">
          {{ row.name }}
        </p>
        <UBadge color="neutral" variant="subtle" size="sm">
          {{ row.multiple ? 'many' : 'one' }}
        </UBadge>
        <span v-if="row.images.length" class="text-xs text-muted">
          {{ row.images.length }} file{{ row.images.length === 1 ? '' : 's' }}
        </span>
      </div>

      <div class="flex flex-wrap gap-2">
        <EntityRoleBox
          v-for="(image, index) in row.images"
          :key="`${row.name}-${image.id}`"
          :image="image"
          :role="row.name"
          :highlight="!row.multiple"
          :replaceable="!row.multiple"
          :can-move-earlier="row.multiple && index > 0"
          :can-move-later="row.multiple && index < row.images.length - 1"
          :busy="busy === image.id"
          @pick="picking = row.name"
          @detach="onDetach(image)"
          @move="onMove(row.name, image, $event)"
        />

        <!-- A single-valued role that is already filled offers Replace on the box itself, so no
             trailing empty box; a multi-valued one always has somewhere to append. -->
        <EntityRoleBox
          v-if="row.multiple || !row.images.length"
          :role="row.name"
          :busy="busy === `new-${row.name}`"
          @pick="picking = row.name"
        />
      </div>

      <p v-if="row.images.some(i => !i.altText)" class="text-xs text-warning">
        <UIcon name="i-lucide-triangle-alert" class="size-3.5" />
        Some of these have no alt text in this language — add it from the Media page.
      </p>
    </div>

    <p v-if="!rows.length" class="text-sm text-muted">
      This entity declares no media roles.
    </p>

    <MediaPickerModal
      :open="picking !== null"
      :owner="owner"
      :fixed-role="picking ?? undefined"
      :title="`Attach as ${picking}`"
      :version="mediaVersion"
      @update:open="picking = null"
      @attach="onAttach"
      @changed="emit('changed')"
    />
  </div>
</template>
