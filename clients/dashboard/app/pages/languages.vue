<script setup lang="ts">
import type { DropdownMenuItem, TableColumn } from '@nuxt/ui'
import type { Language } from '~/types'

useHead({ title: 'Languages — DotNetBoost Sample' })

const { languages, pending, loadError, busy, load, isBase, update, setDefault, remove, purge } = useLanguages()

const formOpen = ref(false)
const editing = ref<Language>()

/** The destructive action awaiting confirmation. */
const confirming = ref<{ kind: 'delete' | 'purge', language: Language } | null>(null)

const columns: TableColumn<Language>[] = [
  { accessorKey: 'code', header: 'Code' },
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'direction', header: 'Direction' },
  { id: 'role', header: 'Role' },
  { accessorKey: 'isActive', header: 'Active' },
  { id: 'actions', header: '' }
]

function add() {
  editing.value = undefined
  formOpen.value = true
}

function edit(language: Language) {
  editing.value = language
  formOpen.value = true
}

function toggleActive(language: Language, isActive: boolean) {
  // PUT is a full replacement, so the name and direction go back as they are.
  update(language.code, { name: language.name, direction: language.direction, isActive })
}

function actions(language: Language): DropdownMenuItem[][] {
  return [[{
    label: 'Edit',
    icon: 'i-lucide-pencil',
    onSelect: () => edit(language)
  }, {
    label: 'Make default',
    icon: 'i-lucide-star',
    disabled: language.isDefault || !language.isActive,
    onSelect: () => setDefault(language.code)
  }], [{
    label: 'Delete its translations',
    icon: 'i-lucide-eraser',
    color: 'error',
    onSelect: () => confirming.value = { kind: 'purge', language }
  }, {
    label: 'Delete',
    icon: 'i-lucide-trash-2',
    color: 'error',
    disabled: language.isDefault,
    onSelect: () => confirming.value = { kind: 'delete', language }
  }]]
}

const confirmation = computed(() => {
  const target = confirming.value

  if (!target) {
    return { title: '', description: '', label: '' }
  }

  return target.kind === 'purge'
    ? {
        title: `Delete everything written in ${target.language.name}?`,
        description: [
          `Every translation in ${target.language.code} is removed, across every entity. The`,
          'language itself stays, and content falls back to the default until it is filled in again.',
          isBase(target.language.code)
            ? 'This is the base language, so its values live in the entities\' own columns and are not touched.'
            : ''
        ].filter(Boolean).join(' '),
        label: 'Delete translations'
      }
    : {
        title: `Delete ${target.language.name}?`,
        description: 'The language and everything written in it go. The API refuses if it is the default.',
        label: 'Delete'
      }
})

async function onConfirm() {
  const target = confirming.value

  if (!target) {
    return
  }

  await (target.kind === 'purge' ? purge(target.language.code) : remove(target.language.code))
  confirming.value = null
}

onMounted(load)
</script>

<template>
  <UDashboardPanel id="languages">
    <template #header>
      <UDashboardNavbar title="Languages" :ui="{ right: 'gap-3' }">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>

        <template #right>
          <UButton
            icon="i-lucide-refresh-cw"
            color="neutral"
            variant="ghost"
            :loading="pending"
            aria-label="Reload the languages"
            @click="load"
          />
          <UButton icon="i-lucide-plus" label="Add language" @click="add" />
          <LanguageSelect />
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <UPageCard
        title="Languages"
        description="Served by MapLanguageEndpoints() from DotNetBoost.MultiLingual.API — library endpoints, not ones this application wrote. Direction is stored alongside the language, which is what lets the shell flip to RTL without a hardcoded list of codes in the client."
        variant="naked"
      >
        <UBadge
          color="neutral"
          variant="subtle"
          class="w-fit font-mono"
          label="api/languages"
        />
      </UPageCard>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-plug-zap"
        title="Could not load the languages"
        :description="loadError"
        :actions="[{ label: 'Try again', color: 'neutral', variant: 'outline', onClick: load }]"
      />

      <UTable
        v-else
        :data="languages"
        :columns="columns"
        :loading="pending && !languages.length"
        empty="No languages yet. Add one to start writing content."
        class="rounded-lg border border-default"
      >
        <template #code-cell="{ row }">
          <span class="font-mono text-highlighted">{{ row.original.code }}</span>
        </template>

        <template #name-cell="{ row }">
          <span :dir="row.original.direction" class="text-highlighted">{{ row.original.name }}</span>
        </template>

        <template #direction-cell="{ row }">
          <UBadge
            color="neutral"
            variant="outline"
            class="font-mono"
            :icon="row.original.direction === 'rtl' ? 'i-lucide-pilcrow-left' : 'i-lucide-pilcrow-right'"
            :label="row.original.direction"
          />
        </template>

        <template #role-cell="{ row }">
          <div class="flex flex-wrap gap-1">
            <UBadge
              v-if="row.original.isDefault"
              color="primary"
              variant="subtle"
              icon="i-lucide-star"
              label="default"
            />
            <UTooltip
              v-if="isBase(row.original.code)"
              text="The base language: its values live in the entities' own columns rather than in the translations table."
            >
              <UBadge
                color="info"
                variant="subtle"
                icon="i-lucide-database"
                label="base"
              />
            </UTooltip>
          </div>
        </template>

        <template #isActive-cell="{ row }">
          <USwitch
            :model-value="row.original.isActive"
            :loading="busy === row.original.code"
            :disabled="busy !== null"
            :aria-label="row.original.isActive ? `Deactivate ${row.original.name}` : `Activate ${row.original.name}`"
            @update:model-value="toggleActive(row.original, $event)"
          />
        </template>

        <template #actions-cell="{ row }">
          <div class="flex justify-end">
            <UDropdownMenu :items="actions(row.original)" :content="{ align: 'end' }">
              <UButton
                icon="i-lucide-ellipsis-vertical"
                color="neutral"
                variant="ghost"
                :aria-label="`Actions for ${row.original.name}`"
              />
            </UDropdownMenu>
          </div>
        </template>
      </UTable>

      <LanguagesFormModal v-model:open="formOpen" :language="editing" />

      <ConfirmModal
        :open="confirming !== null"
        :title="confirmation.title"
        :description="confirmation.description"
        :confirm-label="confirmation.label"
        :loading="busy !== null"
        @update:open="!$event && (confirming = null)"
        @confirm="onConfirm"
      />
    </template>
  </UDashboardPanel>
</template>
