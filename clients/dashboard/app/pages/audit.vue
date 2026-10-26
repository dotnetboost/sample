<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import type { AuditAction, AuditEntry, AuditSort } from '~/types'

/**
 * DotNetBoost.Auditing's trail, as the one page in this dashboard that has to be signed in to.
 *
 * Two things about it are not like the other three libraries' pages, and both are the library's
 * own decisions rather than this sample's:
 *
 *   • **it needs a caller.** `MapAuditEndpoints()` requires an authenticated one by default — a
 *     trail says who did what and when, which is personal data about an organisation's staff — and
 *     the sample keeps that default instead of turning it off. The API's sample scheme signs every
 *     request in as one sample user, so the page is always readable here;
 *   • **translated text is not in it.** Name and Description carry `[AuditIgnore]`, so a save that
 *     only changed Arabic text adds no entry. The rules card says that in the model's own words,
 *     next to the trail, because the alternative is someone concluding the recording is broken.
 */
const { t } = useI18n()

useHead({ title: () => t('nav.documentTitle', { page: t('nav.audit') }) })

const {
  query, entries, entities, maxPageSize, schema,
  pending, page, total, isFiltered, loadError,
  setFilters, setPageSize, goTo, showEdit, reset, reload
} = useAuditTrail()

/**
 * How the rows on screen are ordered.
 *
 * Over this page only, and that is the API's limit rather than a choice: the trail is served newest
 * first with no parameter for another order. So "oldest first" is the oldest of these rows, and
 * page 1 is still the newest entries.
 */
const sort = ref<AuditSort>('newest')

const sortedEntries = computed(() => {
  const rows = [...entries.value]
  // Newest first is the order the API already answered in, so the tiebreak for every other sort.
  const byKey = (key: (entry: AuditEntry) => string) =>
    rows.sort((a, b) => key(a).localeCompare(key(b)) || b.occurredAt.localeCompare(a.occurredAt))

  switch (sort.value) {
    case 'oldest': return rows.reverse()
    case 'entity': return byKey(entry => `${entry.entityName}\u0000${entry.entityId}`)
    case 'action': return byKey(entry => entry.action)
    case 'actor': return byKey(entry => entry.actor ?? '\uffff')
    default: return rows
  }
})

/** Which rows are open, by entry id. `UTable`'s own expansion state. */
const expanded = ref<Record<string, boolean>>({})

// Computed: a plain array would freeze the headers in whatever language the table first drew in.
const columns = computed<TableColumn<AuditEntry>[]>(() => [
  { id: 'expand', header: '' },
  { accessorKey: 'occurredAt', header: t('audit.columns.occurredAt') },
  { accessorKey: 'entityName', header: t('audit.columns.entity') },
  { accessorKey: 'action', header: t('audit.columns.action') },
  { accessorKey: 'actor', header: t('audit.columns.actor') },
  { id: 'changes', header: t('audit.columns.changes') }
])

const actionColors: Record<AuditAction, 'success' | 'info' | 'error'> = {
  Created: 'success',
  Updated: 'info',
  Deleted: 'error'
}

const actionIcons: Record<AuditAction, string> = {
  Created: 'i-lucide-plus',
  Updated: 'i-lucide-pencil',
  Deleted: 'i-lucide-trash-2'
}

/**
 * The action, in words. Computed rather than looked up by a key built from the value: a key
 * assembled at runtime is one `typedOptionsAndMessages` cannot check, and the three actions are
 * the whole set.
 */
const actionLabels = computed<Record<AuditAction, string>>(() => ({
  Created: t('audit.actions.created'),
  Updated: t('audit.actions.updated'),
  Deleted: t('audit.actions.deleted')
}))

/**
 * Filtering to one entity's key, from a row.
 *
 * The usual way into a trail is "what happened to this one row", and typing a key read off the
 * screen into the box above is the long way round.
 */
function showEntity(entry: AuditEntry) {
  setFilters({ entityName: entry.entityName, entityId: entry.entityId, correlationId: '' })
}

/** Paging closes whatever was open: a row id on one page means nothing on the next. */
async function go(move: () => unknown) {
  expanded.value = {}
  await move()
}

/** "1–25 of 140", or nothing for an empty trail. */
const range = computed(() => {
  if (!total.value) {
    return ''
  }

  const size = query.pageSize

  return t('audit.pager.range', {
    from: (page.value - 1) * size + 1,
    to: Math.min(page.value * size, total.value),
    total: total.value
  })
})
</script>

<template>
  <UDashboardPanel id="audit">
    <template #header>
      <UDashboardNavbar :title="$t('nav.audit')" :ui="{ right: 'gap-3' }">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>

        <template #right>
          <UButton
            icon="i-lucide-refresh-cw"
            color="neutral"
            variant="ghost"
            :loading="pending"
            :aria-label="$t('audit.reload')"
            @click="reload()"
          />
          <LanguageSelect />
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <UPageCard
        :title="$t('audit.title')"
        :description="$t('audit.card.description')"
        variant="naked"
      >
        <UBadge
          color="neutral"
          variant="subtle"
          dir="ltr"
          class="w-fit font-mono"
          label="api/audit"
        />
      </UPageCard>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-plug-zap"
        :title="$t('audit.loadError')"
        :description="loadError"
        :actions="[{ label: $t('common.tryAgain'), color: 'neutral', variant: 'outline', onClick: () => reload() }]"
      />

      <template v-else>
        <AuditFilters
          v-model:sort="sort"
          :query="query"
          :entities="entities"
          :max-page-size="maxPageSize"
          :is-filtered="isFiltered"
          :pending="pending"
          @update="setFilters($event)"
          @page-size="setPageSize($event)"
          @reset="reset()"
        />

        <!-- Only with an entity filtered on: "what is recorded for Product" has no answer across
             entities, and one averaged over two would be a statement the model never made. -->
        <AuditEntityRules v-if="schema" :schema="schema" />

        <UAlert
          v-if="query.correlationId"
          color="info"
          variant="subtle"
          icon="i-lucide-git-compare-arrows"
          :title="$t('audit.editFilter.title')"
          :description="$t('audit.editFilter.description')"
          :actions="[{
            label: $t('common.clear'),
            color: 'neutral',
            variant: 'outline',
            onClick: () => setFilters({ correlationId: '' })
          }]"
        />

        <!-- `shrink-0`: the panel body is a scrolling flex column and the table's root scrolls on
             its own, so once the rules card appeared the table gave up its height and became a
             few rows in a scroll box of its own. The page scrolls; the table only does sideways. -->
        <UTable
          v-model:expanded="expanded"
          :data="sortedEntries"
          :columns="columns"
          :loading="pending && !entries.length"
          :empty="isFiltered ? $t('audit.emptyFiltered') : $t('audit.empty')"
          :get-row-id="(entry: AuditEntry) => entry.id"
          :expanded-options="{ getRowCanExpand: () => true }"
          class="shrink-0 rounded-lg border border-default"
        >
          <template #expand-cell="{ row }">
            <UButton
              color="neutral"
              variant="ghost"
              size="sm"
              :icon="row.getIsExpanded() ? 'i-lucide-chevron-down' : 'i-lucide-chevron-right'"
              :aria-label="$t('audit.expand')"
              :aria-expanded="row.getIsExpanded()"
              @click="row.toggleExpanded()"
            />
          </template>

          <template #occurredAt-cell="{ row }">
            <!-- Relative, with the exact instant a hover away: "3 hours ago" is what a trail is
                 read with, and the timestamp is what it is quoted with. Both through
                 `utils/format.ts`, so both are in the UI locale. -->
            <UTooltip :text="formatDate(row.original.occurredAt)">
              <span class="whitespace-nowrap text-highlighted">
                {{ formatRelative(row.original.occurredAt) }}
              </span>
            </UTooltip>
          </template>

          <template #entityName-cell="{ row }">
            <UButton
              color="neutral"
              variant="link"
              class="p-0 font-mono"
              :aria-label="$t('audit.filterToEntity', {
                entity: row.original.entityName, id: row.original.entityId
              })"
              @click="showEntity(row.original)"
            >
              <span dir="ltr">{{ row.original.entityName }}&nbsp;{{ row.original.entityId }}</span>
            </UButton>
          </template>

          <template #action-cell="{ row }">
            <UBadge
              :color="actionColors[row.original.action]"
              :icon="actionIcons[row.original.action]"
              variant="subtle"
              :label="actionLabels[row.original.action]"
            />
          </template>

          <template #actor-cell="{ row }">
            <!--
              A null actor is nobody — not "system". The library says so where the field is
              documented, and this is the renderer that has to mean it: nothing was captured, so
              the entry names no one, and the tooltip says that rather than leaving "unknown" to be
              read as an account name.
            -->
            <UTooltip v-if="!row.original.actor" :text="$t('audit.unknownActorTooltip')">
              <span class="italic text-dimmed">{{ $t('audit.unknownActor') }}</span>
            </UTooltip>
            <span v-else dir="ltr" class="text-highlighted">{{ row.original.actor }}</span>
          </template>

          <template #changes-cell="{ row }">
            <span class="whitespace-nowrap text-muted">
              {{ $t('audit.changeCount', row.original.changes.length) }}
            </span>
          </template>

          <template #expanded="{ row }">
            <AuditEntryChanges :entry="row.original" @show-edit="go(() => showEdit($event))" />
          </template>
        </UTable>

        <div v-if="total" class="flex flex-wrap items-center justify-between gap-3">
          <span class="text-sm text-muted">{{ range }}</span>

          <UPagination
            v-if="total > query.pageSize"
            :page="page"
            :total="total"
            :items-per-page="query.pageSize"
            :disabled="pending"
            @update:page="go(() => goTo($event))"
          />
        </div>
      </template>
    </template>
  </UDashboardPanel>
</template>
