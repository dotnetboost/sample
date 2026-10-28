import type { ColumnVisibilityState, SortingState, Updater } from '@tanstack/vue-table'
import {
  columnVisibilityFeature,
  createColumnHelper,
  rowSortingFeature,
  tableFeatures,
  useTable
} from '@tanstack/vue-table'
import type { MediaFile, MediaQuery, MediaSortBy } from '~/types'

/**
 * The media library's list view, as a TanStack table — the Catalog and Audit tables' recipe with
 * one difference that matters: **the sort is the API's**, not the page's.
 *
 * `GET api/media` takes `sortBy` and `descending`, so a header here does not reorder the rows on
 * screen; it writes the query, and `useMediaLibrary` goes back to page 1 and asks again. That is
 * TanStack's `manualSorting`: the table holds the state and draws the arrows, and trusts that the
 * rows it is handed are already in that order. It also means a sort covers the whole library, so
 * this view needs no "within this page" caveat.
 *
 * Only the three orders the API serves are sortable — name, size, upload time. Kind, category and
 * tags are not, because asking for them would be asking the API for something it does not do.
 *
 * Built here rather than inside the table component, so the browser around it can put the table's
 * Columns menu in its own filter bar without reaching into a child.
 */
const features = tableFeatures({ rowSortingFeature, columnVisibilityFeature })

const helper = createColumnHelper<typeof features, MediaFile>()

const columns = helper.columns([
  helper.accessor('fileName', { enableHiding: false }),
  helper.accessor('contentType', { id: 'kind', enableSorting: false }),
  helper.accessor('category', { enableSorting: false }),
  helper.accessor('size', { sortDescFirst: true }),
  helper.accessor('createdAt', { sortDescFirst: true }),
  helper.display({ id: 'actions', enableSorting: false, enableHiding: false })
])

/** The table's column ids that are also API sort keys — every sortable column is one. */
const sortKeys: Record<string, MediaSortBy> = {
  fileName: 'fileName',
  size: 'size',
  createdAt: 'createdAt'
}

function resolve<T>(next: Updater<T>, current: T): T {
  return typeof next === 'function' ? (next as (old: T) => T)(current) : next
}

export function useMediaFileTable(
  items: Ref<MediaFile[]>,
  query: MediaQuery,
  onSort: (patch: Pick<MediaQuery, 'sortBy' | 'descending'>) => void
) {
  const { t } = useI18n()

  /**
   * The query's order as TanStack's state. `sortOrder` — a role's own order, which the picker's
   * "attached" tab asks for — has no column, so it shows as no arrow at all.
   */
  const sorting = computed<SortingState>(() =>
    query.sortBy in sortKeys ? [{ id: query.sortBy, desc: query.descending }] : [])

  // Remembered per browser, as the other tables' are.
  const columnVisibility = useLocalStorage<ColumnVisibilityState>('media-table-columns', {})

  const table = useTable({
    features,
    columns,
    data: items,
    getRowId: (file: MediaFile) => file.id,
    manualSorting: true,
    enableSortingRemoval: false,
    state: computed(() => ({ sorting: sorting.value, columnVisibility: columnVisibility.value })),
    onSortingChange: (next) => {
      const [first] = resolve(next, sorting.value)
      const sortBy = first && sortKeys[first.id]
      if (sortBy) onSort({ sortBy, descending: first.desc })
    },
    onColumnVisibilityChange: next => columnVisibility.value = resolve(next, columnVisibility.value)
  })

  const hideable = computed(() => table.getAllLeafColumns().filter(column => column.getCanHide()))

  const labels = computed<Record<string, string>>(() => ({
    fileName: t('media.list.columns.file'),
    kind: t('media.list.columns.kind'),
    category: t('media.list.columns.category'),
    size: t('media.list.columns.size'),
    createdAt: t('media.list.columns.uploaded')
  }))

  return { table, hideable, labels }
}
