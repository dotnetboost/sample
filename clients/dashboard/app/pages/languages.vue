<script setup lang="ts">
import type { DropdownMenuItem, TableColumn } from '@nuxt/ui'
import type { Language } from '~/types'

const { t } = useI18n()

useHead({ title: () => t('nav.documentTitle', { page: t('nav.languages') }) })

const { languages, pending, loadError, busy, load, isBase, update, setDefault, remove, purge } = useLanguages()

const formOpen = ref(false)
const editing = ref<Language>()

/** The destructive action awaiting confirmation. */
const confirming = ref<{ kind: 'delete' | 'purge', language: Language } | null>(null)

// Computed: a plain array would freeze the headers in whatever language the table first drew in.
const columns = computed<TableColumn<Language>[]>(() => [
  { accessorKey: 'code', header: t('languages.columns.code') },
  { accessorKey: 'name', header: t('languages.columns.name') },
  { accessorKey: 'direction', header: t('languages.columns.direction') },
  { id: 'role', header: t('languages.columns.role') },
  { accessorKey: 'isActive', header: t('languages.columns.active') },
  { id: 'actions', header: '' }
])

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
    label: t('languages.actions.edit'),
    icon: 'i-lucide-pencil',
    onSelect: () => edit(language)
  }, {
    label: t('languages.actions.makeDefault'),
    icon: 'i-lucide-star',
    disabled: language.isDefault || !language.isActive,
    onSelect: () => setDefault(language.code)
  }], [{
    label: t('languages.actions.purge'),
    icon: 'i-lucide-eraser',
    color: 'error',
    onSelect: () => confirming.value = { kind: 'purge', language }
  }, {
    label: t('languages.actions.delete'),
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
        title: t('languages.confirmPurge.title', { name: target.language.name }),
        // The base-language sentence is a second message rather than a branch inside the first:
        // a translator should not have to assemble one paragraph out of a conditional.
        description: [
          t('languages.confirmPurge.description', { code: target.language.code }),
          isBase(target.language.code) ? t('languages.confirmPurge.baseNote') : ''
        ].filter(Boolean).join(' '),
        label: t('languages.confirmPurge.confirm')
      }
    : {
        title: t('languages.confirmDelete.title', { name: target.language.name }),
        description: t('languages.confirmDelete.description'),
        label: t('common.delete')
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
      <UDashboardNavbar :title="$t('nav.languages')" :ui="{ right: 'gap-3' }">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>

        <template #right>
          <UButton
            icon="i-lucide-refresh-cw"
            color="neutral"
            variant="ghost"
            :loading="pending"
            :aria-label="$t('languages.reload')"
            @click="load"
          />
          <UButton icon="i-lucide-plus" :label="$t('languages.add')" @click="add" />
          <LanguageSelect />
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <UPageCard
        :title="$t('languages.title')"
        :description="$t('languages.card.description')"
        variant="naked"
      >
        <UBadge
          color="neutral"
          variant="subtle"
          dir="ltr"
          class="w-fit font-mono"
          label="api/languages"
        />
      </UPageCard>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-plug-zap"
        :title="$t('languages.loadError')"
        :description="loadError"
        :actions="[{ label: $t('common.tryAgain'), color: 'neutral', variant: 'outline', onClick: load }]"
      />

      <UTable
        v-else
        :data="languages"
        :columns="columns"
        :loading="pending && !languages.length"
        :empty="$t('languages.empty')"
        class="rounded-lg border border-default"
      >
        <template #code-cell="{ row }">
          <!-- `dir="ltr"`: a language tag is an identifier, even on a right-to-left page. -->
          <span dir="ltr" class="font-mono text-highlighted">{{ row.original.code }}</span>
        </template>

        <template #name-cell="{ row }">
          <span :dir="row.original.direction" class="text-highlighted">{{ row.original.name }}</span>
        </template>

        <template #direction-cell="{ row }">
          <UBadge
            color="neutral"
            variant="outline"
            dir="ltr"
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
              :label="$t('languages.badge.default')"
            />
            <UTooltip
              v-if="isBase(row.original.code)"
              :text="$t('languages.badge.baseTooltip')"
            >
              <UBadge
                color="info"
                variant="subtle"
                icon="i-lucide-database"
                :label="$t('languages.badge.base')"
              />
            </UTooltip>
          </div>
        </template>

        <template #isActive-cell="{ row }">
          <USwitch
            :model-value="row.original.isActive"
            :loading="busy === row.original.code"
            :disabled="busy !== null"
            :aria-label="row.original.isActive
              ? $t('languages.deactivate', { name: row.original.name })
              : $t('languages.activate', { name: row.original.name })"
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
                :aria-label="$t('languages.actionsFor', { name: row.original.name })"
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
