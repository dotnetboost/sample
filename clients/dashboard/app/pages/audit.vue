<script setup lang="ts">
import type { Component } from 'vue'
import type { ColumnVisibilityState, SortingState, Updater } from '@tanstack/vue-table'
import {
  ChevronRight, GitCompareArrows, Pencil, PlugZap, Plus, RefreshCw, ScrollText, Trash2
} from '@lucide/vue'
import {
  columnVisibilityFeature,
  createColumnHelper,
  createSortedRowModel,
  rowSortingFeature,
  sortFn_basic,
  sortFn_text,
  tableFeatures,
  useTable
} from '@tanstack/vue-table'
import type { AuditAction, AuditEntry } from '~/types'

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
 * The table, on TanStack Table — which owns two things here and no more: the order of the rows on
 * screen, and which columns are shown. The markup stays this file's; TanStack is headless.
 *
 * **Sorting is over this page only**, and that is the API's limit rather than a choice: the trail
 * is served newest first with no parameter for another order. TanStack's own guidance calls
 * client sorting of one server page a mistake when the order is meant to span the dataset, and
 * here it is not meant to — so the sorted header says "within this page" under the table rather
 * than letting "oldest first" pass for the oldest entries in the trail. Page 1 is still the newest.
 *
 * Its sort is stable, so rows that tie keep the order the API sent — newest first — which is the
 * tiebreak the hand-written sort this replaces had to write out.
 *
 * Features and columns are module-level constants, as the library asks: rebuilding either on a
 * render invalidates every row model.
 */
const features = tableFeatures({
  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
  sortFns: { basic: sortFn_basic, text: sortFn_text },
  columnVisibilityFeature
})

const helper = createColumnHelper<typeof features, AuditEntry>()

const columns = helper.columns([
  helper.display({ id: 'expand', enableSorting: false, enableHiding: false }),
  // ISO 8601 in UTC sorts as text in time order, which is the point of the format.
  helper.accessor('occurredAt', { sortFn: 'text', enableHiding: false }),
  helper.accessor(entry => `${entry.entityName}\u0000${entry.entityId}`, { id: 'entity', sortFn: 'text' }),
  helper.accessor('action', { sortFn: 'text' }),
  // An entry with no actor names nobody, so it sorts after everyone who is named, either way up.
  helper.accessor(entry => entry.actor ?? undefined, { id: 'actor', sortFn: 'text', sortUndefined: 'last' }),
  helper.accessor(entry => entry.changes.length, { id: 'changes', sortFn: 'basic' })
])

/** The API's own order. A column is always sorted — the cycle is asc ↔ desc, never "off". */
const newestFirst: SortingState = [{ id: 'occurredAt', desc: true }]

const sorting = ref<SortingState>(newestFirst)

/**
 * Which columns are shown, remembered per browser: it is how one reader likes the table laid out,
 * not something worth a setting. Only an explicit `false` hides a column, so an empty object is
 * "everything", and a column added later starts out visible.
 */
const columnVisibility = useLocalStorage<ColumnVisibilityState>('audit-table-columns', {})

/** TanStack hands state changes over as a value or as a function of the old one. */
function resolve<T>(next: Updater<T>, current: T): T {
  return typeof next === 'function' ? (next as (old: T) => T)(current) : next
}

const table = useTable({
  features,
  columns,
  data: entries,
  getRowId: (entry: AuditEntry) => entry.id,
  enableSortingRemoval: false,
  state: computed(() => ({ sorting: sorting.value, columnVisibility: columnVisibility.value })),
  onSortingChange: next => sorting.value = resolve(next, sorting.value),
  onColumnVisibilityChange: next => columnVisibility.value = resolve(next, columnVisibility.value)
})

const rows = computed(() => table.getRowModel().rows)

/** Whether a column is on. The template draws each cell by hand, so it asks per column. */
const shown = (id: string) => table.getColumn(id)?.getIsVisible() ?? true

/** The column for a header, which `SortableHead` drives. Every id asked for is one defined above. */
const column = (id: string) => table.getColumn(id)!

const isNewestFirst = computed(() =>
  sorting.value.length === 1 && sorting.value[0]!.id === 'occurredAt' && sorting.value[0]!.desc)

/** The columns the Columns menu offers. `When` and the chevron are the row; they stay. */
const hideable = computed(() => table.getAllLeafColumns().filter(c => c.getCanHide()))

const columnLabels = computed<Record<string, string>>(() => ({
  occurredAt: t('audit.columns.occurredAt'),
  entity: t('audit.columns.entity'),
  action: t('audit.columns.action'),
  actor: t('audit.columns.actor'),
  changes: t('audit.columns.changes')
}))

/** How many columns a full-width cell spans: whatever is on, the chevron included. */
const span = computed(() => table.getVisibleLeafColumns().length)

/**
 * The page sizes the API will actually serve, which is why `maxPageSize` is read rather than
 * assumed. A size above the cap is not refused — it is clamped — so offering one would leave the
 * control disagreeing with the `take` the response reports.
 */
const pageSizes = computed(() =>
  [10, 25, 50, 100].filter(size => !maxPageSize.value || size <= maxPageSize.value))

const pageCount = computed(() => Math.max(1, Math.ceil(total.value / query.pageSize)))

/** Which rows are open, by entry id. */
const expanded = ref<Record<string, boolean>>({})

/**
 * The action's icon colour, inside an outline badge as the `dashboard-01` block draws a status:
 * green for a row that appeared, the accent for a change, red for a removal. The colour is on the
 * icon alone, so a column of them reads as a list of words with a marker each, not as a wall of
 * coloured pills.
 */
const actionStyles: Record<AuditAction, string> = {
  Created: 'text-success',
  Updated: 'text-primary',
  Deleted: 'text-destructive'
}

const actionIcons: Record<AuditAction, Component> = {
  Created: Plus,
  Updated: Pencil,
  Deleted: Trash2
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

/**
 * Paging closes whatever was open: a row id on one page means nothing on the next. The sort stays,
 * as it does in any table — it is still over the page on screen, and the note under the table
 * still says so.
 */
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
  <DashboardPage :title="$t('nav.audit')">
    <template #actions>
      <Button
        variant="ghost"
        size="icon-sm"
        :disabled="pending"
        :aria-label="$t('audit.reload')"
        @click="reload()"
      >
        <RefreshCw :class="{ 'animate-spin': pending }" />
      </Button>
    </template>

    <SectionIntro :title="$t('audit.title')" :description="$t('audit.card.description')">
      <Badge variant="outline" dir="ltr">
        api/audit
      </Badge>
    </SectionIntro>

    <Alert v-if="loadError" variant="destructive">
      <PlugZap />
      <AlertTitle>{{ $t('audit.loadError') }}</AlertTitle>
      <AlertDescription>{{ loadError }}</AlertDescription>
      <AlertAction>
        <Button variant="outline" size="sm" @click="() => reload()">
          {{ $t('common.tryAgain') }}
        </Button>
      </AlertAction>
    </Alert>

    <template v-else>
      <AuditFilters
        :query="query"
        :entities="entities"
        :is-filtered="isFiltered"
        :pending="pending"
        @update="setFilters($event)"
        @reset="reset()"
      >
        <!-- Over the table's end corner: which columns this reader wants, kept per browser. -->
        <div class="flex justify-end sm:ms-auto">
          <ColumnsMenu :columns="hideable" :labels="columnLabels" />
        </div>
      </AuditFilters>

      <!-- Only with an entity filtered on: "what is recorded for Product" has no answer across
           entities, and one averaged over two would be a statement the model never made. -->
      <AuditEntityRules v-if="schema" :schema="schema" />

      <Alert v-if="query.correlationId" class="border-primary/30 bg-primary/5">
        <GitCompareArrows class="text-primary" />
        <AlertTitle>{{ $t('audit.editFilter.title') }}</AlertTitle>
        <AlertDescription>{{ $t('audit.editFilter.description') }}</AlertDescription>
        <AlertAction>
          <Button variant="outline" size="sm" @click="setFilters({ correlationId: '' })">
            {{ $t('common.clear') }}
          </Button>
        </AlertAction>
      </Alert>

      <!-- A row opens and closes wherever it is clicked; the chevron is the same toggle as a real
           button, for the keyboard, and the entity link stops the click so following it does not
           also unfold the row.

           Each cell is drawn by hand and asks `shown()` whether its column is on, rather than
           looping over TanStack's visible cells: the cells differ too much for one loop to read
           well, and the responsive `hidden lg:table-cell` on top of the menu's choice is a class,
           not table state. -->
      <TableFrame>
        <Table>
          <TableHeader class="sticky-head">
            <TableRow>
              <TableHead class="w-10 ps-2" />
              <SortableHead :column="column('occurredAt')" :label="columnLabels.occurredAt!" />
              <SortableHead v-if="shown('entity')" :column="column('entity')" :label="columnLabels.entity!" />
              <SortableHead v-if="shown('action')" :column="column('action')" :label="columnLabels.action!" />
              <SortableHead
                v-if="shown('actor')"
                :column="column('actor')"
                :label="columnLabels.actor!"
                class="hidden lg:table-cell"
              />
              <SortableHead
                v-if="shown('changes')"
                :column="column('changes')"
                :label="columnLabels.changes!"
                align="end"
                class="hidden pe-4 md:table-cell"
              />
            </TableRow>
          </TableHeader>

          <TableBody>
            <template v-if="pending && !entries.length">
              <!-- One bar across the row rather than one per cell: how many cells a row shows
                   depends on the screen, and a placeholder should not have to know. -->
              <TableRow v-for="n in 6" :key="n">
                <TableCell :colspan="span" class="px-4">
                  <Skeleton class="h-5 w-full" />
                </TableCell>
              </TableRow>
            </template>

            <TableEmpty v-else-if="!entries.length" :colspan="span">
              <Empty>
                <EmptyHeader>
                  <EmptyMedia variant="icon">
                    <ScrollText />
                  </EmptyMedia>
                  <EmptyTitle>{{ isFiltered ? $t('audit.emptyFiltered') : $t('audit.empty') }}</EmptyTitle>
                </EmptyHeader>
              </Empty>
            </TableEmpty>

            <template v-for="row in rows" v-else :key="row.id">
              <TableRow
                :data-state="expanded[row.id] ? 'selected' : undefined"
                class="cursor-pointer"
                @click="expanded[row.id] = !expanded[row.id]"
              >
                <TableCell class="ps-2">
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    :aria-label="$t('audit.expand')"
                    :aria-expanded="!!expanded[row.id]"
                    @click.stop="expanded[row.id] = !expanded[row.id]"
                  >
                    <ChevronRight
                      class="transition-transform rtl:rotate-180"
                      :class="{ 'rotate-90!': expanded[row.id] }"
                    />
                  </Button>
                </TableCell>

                <TableCell>
                  <!-- Relative, with the exact instant a hover away: "3 hours ago" is what a trail is
                       read with, and the timestamp is what it is quoted with. Both through
                       `utils/format.ts`, so both are in the UI locale. -->
                  <Tooltip>
                    <TooltipTrigger as-child>
                      <span class="whitespace-nowrap font-medium">
                        {{ formatRelative(row.original.occurredAt) }}
                      </span>
                    </TooltipTrigger>
                    <TooltipContent>{{ formatDate(row.original.occurredAt) }}</TooltipContent>
                  </Tooltip>
                </TableCell>

                <TableCell v-if="shown('entity')">
                  <!-- Truncated, with the whole of it on hover: a media file's key is a GUID, and
                       one of those at full width would push every other column off the table. -->
                  <Button
                    variant="link"
                    class="h-auto max-w-32 p-0 sm:max-w-56 lg:max-w-80"
                    :title="`${row.original.entityName} ${row.original.entityId}`"
                    :aria-label="$t('audit.filterToEntity', {
                      entity: row.original.entityName, id: row.original.entityId
                    })"
                    @click.stop="showEntity(row.original)"
                  >
                    <span dir="ltr" class="truncate">{{ row.original.entityName }}&nbsp;{{ row.original.entityId }}</span>
                  </Button>
                </TableCell>

                <TableCell v-if="shown('action')">
                  <!-- On a phone the icon alone, so the row fits without scrolling sideways; the word
                       stays for assistive technology. -->
                  <Badge variant="outline" class="text-muted-foreground">
                    <component :is="actionIcons[row.original.action]" :class="actionStyles[row.original.action]" />
                    <span class="max-sm:sr-only">{{ actionLabels[row.original.action] }}</span>
                  </Badge>
                </TableCell>

                <TableCell v-if="shown('actor')" class="hidden lg:table-cell">
                  <!--
                    A null actor is nobody — not "system". The library says so where the field is
                    documented, and this is the renderer that has to mean it: nothing was captured,
                    so the entry names no one, and the tooltip says that rather than leaving
                    "unknown" to be read as an account name.
                  -->
                  <Tooltip v-if="!row.original.actor">
                    <TooltipTrigger as-child>
                      <span class="italic text-muted-foreground">{{ $t('audit.unknownActor') }}</span>
                    </TooltipTrigger>
                    <TooltipContent>{{ $t('audit.unknownActorTooltip') }}</TooltipContent>
                  </Tooltip>
                  <span v-else dir="ltr">{{ row.original.actor }}</span>
                </TableCell>

                <TableCell
                  v-if="shown('changes')"
                  class="hidden pe-4 text-end tabular-nums text-muted-foreground md:table-cell"
                >
                  {{ $t('audit.changeCount', row.original.changes.length) }}
                </TableCell>
              </TableRow>

              <TableRow v-if="expanded[row.id]" class="hover:bg-transparent">
                <TableCell :colspan="span" class="bg-muted/30 p-0 whitespace-normal">
                  <AuditEntryChanges :entry="row.original" @show-edit="go(() => showEdit($event))" />
                </TableCell>
              </TableRow>
            </template>
          </TableBody>
        </Table>

        <!--
          The block's table footer, inside the frame: where the rows are and how many there are on
          the start side, and the page controls on the end side. Pages are the API's — every move is
          a request — so these drive `useAuditTrail`, not TanStack's own pagination.
        -->
        <TablePager
          v-if="total"
          :page="page"
          :page-count="pageCount"
          :disabled="pending"
          @update:page="target => go(() => goTo(target))"
        >
          <span class="tabular-nums">{{ range }}</span>
          <!-- Only with more than one page: on a single page a sort covers the whole trail. -->
          <span v-if="!isNewestFirst && pageCount > 1" class="flex items-center gap-1.5 text-xs">
            <span aria-hidden="true">·</span>
            {{ $t('audit.table.sortedThisPage') }}
            <Button
              variant="link"
              size="xs"
              class="h-auto p-0"
              @click="sorting = newestFirst"
            >
              {{ $t('audit.table.resetSort') }}
            </Button>
          </span>

          <template #size>
            <div class="hidden items-center gap-2 sm:flex">
              <Label for="audit-rows-per-page" class="text-sm font-medium">
                {{ $t('common.rowsPerPage') }}
              </Label>
              <Select :model-value="query.pageSize" @update:model-value="go(() => setPageSize(Number($event)))">
                <SelectTrigger id="audit-rows-per-page" size="sm" class="w-20">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent side="top">
                  <SelectItem v-for="size in pageSizes" :key="size" :value="size">
                    {{ size }}
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>
          </template>
        </TablePager>
      </TableFrame>
    </template>
  </DashboardPage>
</template>
