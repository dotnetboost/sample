<script setup lang="ts">
import type { ColumnVisibilityState, SortingState } from '@tanstack/vue-table'
import { ChevronRight, ScrollText } from '@lucide/vue'
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
import type { AuditEntry } from '~/types'

/**
 * One page of the trail, as a table whose rows unfold into what changed, with the pager under it.
 *
 * TanStack Table owns two things here and no more: the order of the rows on screen, and which
 * columns are shown. **Sorting is over this page only**, which is the API's limit rather than a
 * choice: the trail is served newest first with no parameter for another order. So a sorted
 * header says "within this page" under the table rather than letting "oldest first" pass for the
 * oldest entries in the trail. The sort is stable, so ties keep the API's newest-first order.
 *
 * Pages are the API's — every move is a request — so the pager emits, and `useAuditTrail` moves.
 */
const props = defineProps<{
  entries: AuditEntry[]
  pending: boolean
  isFiltered: boolean
  page: number
  total: number
  pageSize: number
  /** The largest page the API serves, or 0 before it has said. */
  maxPageSize: number
}>()

const emit = defineEmits<{
  goTo: [page: number]
  setPageSize: [size: number]
  /** Filter the trail to this entry's entity and key. */
  showEntity: [entry: AuditEntry]
  /** Filter the trail to the edit this correlation id belongs to. */
  showEdit: [correlationId: string]
}>()

const { t } = useI18n()

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
 * Which columns are shown, remembered per browser. Only an explicit `false` hides one, so an empty
 * object is "everything", and a column added later starts out visible.
 */
const columnVisibility = useLocalStorage<ColumnVisibilityState>('audit-table-columns', {})

const table = useTable({
  features,
  columns,
  data: computed(() => props.entries),
  getRowId: (entry: AuditEntry) => entry.id,
  enableSortingRemoval: false,
  state: computed(() => ({ sorting: sorting.value, columnVisibility: columnVisibility.value })),
  onSortingChange: next => sorting.value = applyUpdater(next, sorting.value),
  onColumnVisibilityChange: next => columnVisibility.value = applyUpdater(next, columnVisibility.value)
})

const rows = computed(() => table.getRowModel().rows)

/** Whether a column is on. Each cell is drawn by hand, so it asks per column. */
const shown = (id: string) => table.getColumn(id)?.getIsVisible() ?? true

/** The column for a header, which `SortableHead` drives. Every id asked for is one defined above. */
const column = (id: string) => table.getColumn(id)!

const isNewestFirst = computed(() =>
  sorting.value.length === 1 && sorting.value[0]!.id === 'occurredAt' && sorting.value[0]!.desc)

/** The columns the Columns menu offers. `When` and the chevron are the row; they stay. */
const hideable = computed(() => table.getAllLeafColumns().filter(c => c.getCanHide()))

const labels = computed<Record<string, string>>(() => ({
  occurredAt: t('audit.columns.occurredAt'),
  entity: t('audit.columns.entity'),
  action: t('audit.columns.action'),
  actor: t('audit.columns.actor'),
  changes: t('audit.columns.changes')
}))

/** How many columns a full-width cell spans: whatever is on, the chevron included. */
const span = computed(() => table.getVisibleLeafColumns().length)

/**
 * The page sizes the API will actually serve. A size above its cap is clamped rather than refused,
 * so offering one would leave this control disagreeing with the `take` the response reports.
 */
const pageSizes = computed(() =>
  [10, 25, 50, 100].filter(size => !props.maxPageSize || size <= props.maxPageSize))

const pageCount = computed(() => Math.max(1, Math.ceil(props.total / props.pageSize)))

/** "1–25 of 140", or nothing for an empty trail. */
const range = computed(() => props.total
  ? t('audit.pager.range', {
      from: (props.page - 1) * props.pageSize + 1,
      to: Math.min(props.page * props.pageSize, props.total),
      total: props.total
    })
  : '')

/** Which rows are open, by entry id. */
const expanded = ref<Record<string, boolean>>({})

function toggle(id: string) {
  expanded.value[id] = !expanded.value[id]
}

/**
 * Moving to another page or another question closes whatever was open: a row id on one page
 * means nothing on the next. The sort stays, and the note under the table still says what it
 * covers.
 */
function closeAll() {
  expanded.value = {}
}

function goTo(page: number) {
  closeAll()
  emit('goTo', page)
}

function setPageSize(size: number) {
  closeAll()
  emit('setPageSize', size)
}

function showEdit(correlationId: string) {
  closeAll()
  emit('showEdit', correlationId)
}
</script>

<template>
  <!-- The bar over the table, as the `dashboard-01` block draws one: the slot's controls on the
       start side, which columns on the end. -->
  <div class="flex flex-wrap items-center justify-between gap-2">
    <slot name="toolbar" />
    <ColumnsMenu :columns="hideable" :labels="labels" />
  </div>

  <!-- A row opens and closes wherever it is clicked; the chevron is the same toggle as a real
       button, for the keyboard, and the entity link stops the click so following it does not also
       unfold the row.

       Each cell is drawn by hand and asks `shown()` whether its column is on, rather than looping
       over TanStack's visible cells: the cells differ too much for one loop to read well, and the
       responsive `hidden lg:table-cell` on top of the menu's choice is a class, not table state. -->
  <TableFrame>
    <Table>
      <TableHeader class="sticky-head">
        <TableRow>
          <TableHead class="w-10 ps-2" />
          <SortableHead :column="column('occurredAt')" :label="labels.occurredAt!" />
          <SortableHead v-if="shown('entity')" :column="column('entity')" :label="labels.entity!" />
          <SortableHead v-if="shown('action')" :column="column('action')" :label="labels.action!" />
          <SortableHead
            v-if="shown('actor')"
            :column="column('actor')"
            :label="labels.actor!"
            class="hidden lg:table-cell"
          />
          <SortableHead
            v-if="shown('changes')"
            :column="column('changes')"
            :label="labels.changes!"
            align="end"
            class="hidden pe-4 md:table-cell"
          />
        </TableRow>
      </TableHeader>

      <TableBody>
        <template v-if="pending && !entries.length">
          <!-- One bar across the row rather than one per cell: how many cells a row shows depends
               on the screen, and a placeholder should not have to know. -->
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
            @click="toggle(row.id)"
          >
            <TableCell class="ps-2">
              <Button
                variant="ghost"
                size="icon-sm"
                :aria-label="$t('audit.expand')"
                :aria-expanded="!!expanded[row.id]"
                @click.stop="toggle(row.id)"
              >
                <ChevronRight
                  class="transition-transform rtl:rotate-180"
                  :class="{ 'rotate-90!': expanded[row.id] }"
                />
              </Button>
            </TableCell>

            <TableCell>
              <!-- Relative, with the exact instant a hover away: "3 hours ago" is what a trail is
                   read with, and the timestamp is what it is quoted with. -->
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
              <!-- Truncated, with the whole of it on hover: a media file's key is a GUID, and one
                   of those at full width would push every other column off the table. -->
              <Button
                variant="link"
                class="h-auto max-w-32 p-0 sm:max-w-56 lg:max-w-80"
                :title="`${row.original.entityName} ${row.original.entityId}`"
                :aria-label="$t('audit.filterToEntity', {
                  entity: row.original.entityName, id: row.original.entityId
                })"
                @click.stop="emit('showEntity', row.original)"
              >
                <span dir="ltr" class="truncate">{{ row.original.entityName }}&nbsp;{{ row.original.entityId }}</span>
              </Button>
            </TableCell>

            <TableCell v-if="shown('action')">
              <AuditActionBadge :action="row.original.action" />
            </TableCell>

            <TableCell v-if="shown('actor')" class="hidden lg:table-cell">
              <!-- A null actor is nobody — not "system". Nothing was captured, so the entry names no
                   one, and the tooltip says so rather than leaving "unknown" to read as an account. -->
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
              <AuditEntryChanges :entry="row.original" @show-edit="showEdit" />
            </TableCell>
          </TableRow>
        </template>
      </TableBody>
    </Table>

    <TablePager
      v-if="total"
      :page="page"
      :page-count="pageCount"
      :disabled="pending"
      @update:page="goTo"
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
          <Select :model-value="pageSize" @update:model-value="setPageSize(Number($event))">
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
