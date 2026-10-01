<script setup lang="ts">
import type { MediaFile, MediaPage, MediaRole } from '~/types'

/**
 * The media library in a modal, for attaching a file to something.
 *
 * Two tabs: everything in the library, and what this entity already has. Both are the same
 * `MediaBrowser` the Media page is, so searching, filtering, sorting, paging and uploading all
 * behave identically here — there is no second, cut-down implementation to drift.
 */
const props = defineProps<{
  owner: { entityName: string, entityId: string }
  title?: string
  /**
   * What the attach is for, decided by the host rather than here.
   *
   * The products page opens this from a box that already stands for one role, so the question the
   * role control asks has an answer before the modal is open — and a second control answering it
   * again is one the user can put out of step with the box they clicked. Given, the control is not
   * rendered at all.
   */
  fixedRole?: string
  /**
   * Bumped by the host once an attach it was asked for has actually landed.
   *
   * The attach is the host's to make — it knows its own endpoints — and an emit cannot be awaited,
   * so refreshing straight after emitting would read the state from before the write. This is the
   * host saying "now".
   */
  version?: number
}>()

const emit = defineEmits<{
  attach: [file: MediaFile, role: string, replace: boolean]
  changed: []
}>()

const open = defineModel<boolean>('open', { default: false })

const tab = ref<'library' | 'attached'>('library')

/**
 * What the next attach is for. A role belongs to the link rather than to the file, so it is chosen
 * here, when the link is made, and not on the file itself.
 *
 * Read from the entity rather than hard-coded, because roles are an entity's own vocabulary: a
 * product has a primary image and a gallery, an invoice has an original and a signed copy. This
 * component is opened for whatever the host page is about, so it has to ask.
 *
 * An entity that declares nothing comes back empty and accepts any role — then the box is free text
 * rather than a list, which is exactly what "not declared" means.
 */
const declared = ref<MediaRole[]>([])
const loadingRoles = ref(false)

const role = ref('')

async function loadRoles() {
  loadingRoles.value = true

  try {
    declared.value = await $fetch<MediaRole[]>(`/api/media/roles/${encodeURIComponent(props.owner.entityName)}`)
  } catch {
    // Not worth blocking the picker over: an entity whose roles cannot be read is treated as one
    // that declares none, which is the permissive case and still attaches.
    declared.value = []
  } finally {
    loadingRoles.value = false
  }

  if (props.fixedRole) {
    role.value = props.fixedRole
    return
  }

  // Prefer a multi-valued role as the starting point. Appending is the move that cannot destroy
  // anything, and a picker that opened on "primary" would replace the main image on the first
  // click — a surprising default for a list you are browsing.
  const preferred = declared.value.find(r => r.multiple) ?? declared.value[0]

  role.value = preferred?.name ?? ''
}

const roleOptions = computed(() => declared.value.map(r => ({
  label: r.name,
  value: r.name
})))

/**
 * Whether attaching in the selected role takes it over from whatever held it. A single-valued role
 * replaces; a multi-valued one appends. An undeclared role is treated as appending, because nothing
 * says it holds only one.
 */
const selectedRole = computed(() => {
  const found = declared.value.find(r => r.name === role.value)

  return {
    value: role.value || 'default',
    replace: found ? !found.multiple : false,
    hint: found === undefined
      ? 'This entity declares no roles, so any name will do.'
      : found.multiple
        ? 'Appended to the end, in order.'
        : 'Becomes the one file in this role, replacing whatever held it.'
  }
})

const browse = useTemplateRef('browse')
const attachedBrowser = useTemplateRef('attachedBrowser')

/**
 * What is already attached **in the selected role**, so the browse tab can say so rather than
 * offering it again.
 *
 * Narrowed by role rather than by owner alone, because a file can hold several roles on one entity:
 * a gallery image is a perfectly good candidate for primary, and a badge that only knew the file was
 * attached somewhere would refuse to offer it.
 *
 * Asked of the media API directly rather than taken from whatever the host page calls its images.
 * The sample's product endpoint, for one, only surfaces the `product-images` category — a file
 * attached under any other category is genuinely attached but would be missing from that list,
 * and the browse tab would offer to attach it a second time.
 */
const attachedIds = ref<string[]>([])

async function loadAttached() {
  try {
    const page = await $fetch<MediaPage>('/api/media', {
      query: {
        ownerEntityName: props.owner.entityName,
        ownerEntityId: props.owner.entityId,
        role: selectedRole.value.value,
        take: 500
      }
    })

    attachedIds.value = page.items.map(file => file.id)
  } catch {
    // The browse tab simply offers everything. Attaching is idempotent — the same file in the same
    // role for the same entity is a no-op rather than an error or a second link — and a failed
    // count is not worth an error banner over the library itself.
    attachedIds.value = []
  }
}

// The badge means "already in this role", so changing the role changes which files carry it.
watch(role, () => loadAttached())

// The roles belong to the entity, so a picker reopened for a different one asks again.
watch(() => props.owner.entityName, () => loadRoles(), { immediate: true })

// Reopened from a different box: the same mounted modal, a different role.
watch(() => props.fixedRole, (fixed) => {
  if (fixed) {
    role.value = fixed
  }
})

watch(() => props.version, () => reloadAll())

/** Shared by both modes: what makes attaching safe is that it is a link, not a move. */
const shared = 'A file may belong to several entities at once; attaching it here does not take it off anything else.'

const description = computed(() => (props.fixedRole
  ? `Pick the file this ${props.owner.entityName.toLowerCase()} should use as its ${props.fixedRole}, or upload one — it is stored and attached in one step. ${shared}`
  : `Attach a file that is already in the library, or upload a new one — it is stored and attached in one step. ${shared}`))

const tabs = computed(() => [
  { label: 'All files', value: 'library', icon: 'i-lucide-images' },
  { label: `As ${selectedRole.value.value} (${attachedIds.value.length})`, value: 'attached', icon: 'i-lucide-link' }
])

/** Both tabs show the same rows from different angles, so a change in one has to reach the other. */
async function reloadAll() {
  await loadAttached()
  browse.value?.reload()
  attachedBrowser.value?.reload()
  emit('changed')
}

function onAttach(file: MediaFile) {
  // No refresh here: the host has not written anything yet. `version` is what brings the tabs and
  // the badges up to date once it has.
  emit('attach', file, selectedRole.value.value, selectedRole.value.replace)
}

watch(open, (isOpen) => {
  if (isOpen) {
    loadAttached()
  }
})

onMounted(() => {
  if (open.value) {
    loadAttached()
  }
})
</script>

<template>
  <UModal
    v-model:open="open"
    :title="title ?? `Media for ${owner.entityName} ${owner.entityId}`"
    :description="description"
    :ui="{ content: 'max-w-5xl' }"
  >
    <template #body>
      <div class="flex flex-col gap-4">
        <div class="flex flex-wrap items-end justify-between gap-3">
          <UTabs
            v-model="tab"
            :items="tabs"
            :content="false"
            size="sm"
          />

          <!-- Fixed by the host — the box that opened this already stands for one role, and the
               hint below the tabs says which. -->
          <p v-if="fixedRole && tab === 'library' && !loadingRoles" class="pb-1 text-xs text-muted">
            {{ selectedRole.hint }}
          </p>

          <UFormField
            v-else-if="!fixedRole && tab === 'library'"
            label="Attach as"
            :description="selectedRole.hint"
            :ui="{ description: 'text-xs' }"
          >
            <USkeleton v-if="loadingRoles" class="h-8 w-40" />

            <USelect
              v-else-if="roleOptions.length"
              v-model="role"
              :items="roleOptions"
              value-key="value"
              size="sm"
              class="w-40"
            />

            <!-- Nothing declared, so nothing to choose from: the entity takes any role, and the
                 honest control for that is a box you type in. -->
            <UInput
              v-else
              v-model="role"
              placeholder="default"
              size="sm"
              class="w-40"
              @blur="loadAttached"
            />
          </UFormField>
        </div>

        <!-- Both are kept mounted: switching tabs should not throw away a search you just typed. -->
        <MediaBrowser
          v-show="tab === 'library'"
          ref="browse"
          selectable
          compact
          :attached-ids="attachedIds"
          :upload-owner="owner"
          :upload-role="selectedRole.value"
          :page-size="9"
          @select="onAttach"
          @changed="reloadAll"
        />

        <MediaBrowser
          v-show="tab === 'attached'"
          ref="attachedBrowser"
          compact
          :owner="owner"
          :role="selectedRole.value"
          :upload-owner="owner"
          :upload-role="selectedRole.value"
          :page-size="9"
          @changed="reloadAll"
        />
      </div>
    </template>

    <template #footer>
      <div class="flex w-full justify-end">
        <UButton
          label="Done"
          color="neutral"
          variant="ghost"
          @click="open = false"
        />
      </div>
    </template>
  </UModal>
</template>
