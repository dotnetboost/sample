<script setup lang="ts">
import type { ColumnVisibilityState, SortingState } from '@tanstack/vue-table'
import { FolderTree, Plus } from '@lucide/vue'
import {
  columnVisibilityFeature,
  createColumnHelper,
  createSortedRowModel,
  rowSortingFeature,
  sortFn_alphanumeric,
  sortFn_basic,
  sortFn_text,
  tableFeatures,
  useTable
} from '@tanstack/vue-table'
import type { Category, Language } from '~/types'

/**
 * The Categories tab: the table, its editor, and deleting a row. The list itself is the page's,
 * because the Products tab reads it too; a change here asks the page to re-read it.
 *
 * The API does not page categories, so a sort here sorts every one of them. Unsorted is allowed,
 * unlike on Audit: the API's order is "as added", a real order worth getting back to.
 */
const props = defineProps<{
  categories: Category[]
  languages: Language[]
}>()

const emit = defineEmits<{
  changed: []
}>()

const { t } = useI18n()
const api = useApi()

const editing = ref<number | null>(null)
const editorOpen = ref(false)

function edit(id: number | null) {
  editing.value = id
  editorOpen.value = true
}

const { confirming, deleting, confirm } = useCatalogDelete(
  id => api.categories.remove(id),
  () => emit('changed'))

const features = tableFeatures({
  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
  sortFns: { alphanumeric: sortFn_alphanumeric, basic: sortFn_basic, text: sortFn_text },
  columnVisibilityFeature
})

const helper = createColumnHelper<typeof features, Category>()

const columns = helper.columns([
  helper.accessor('name', { sortFn: 'text', enableHiding: false }),
  helper.accessor('slug', { sortFn: 'alphanumeric' }),
  helper.accessor('products', { sortFn: 'basic', sortDescFirst: true }),
  helper.display({ id: 'actions', enableSorting: false, enableHiding: false })
])

const sorting = ref<SortingState>([])

// Remembered per browser, like the Audit table's: how one reader likes the table laid out.
const visibility = useLocalStorage<ColumnVisibilityState>('catalog-categories-columns', {})

const table = useTable({
  features,
  columns,
  data: computed(() => props.categories),
  getRowId: (category: Category) => String(category.id),
  state: computed(() => ({ sorting: sorting.value, columnVisibility: visibility.value })),
  onSortingChange: next => sorting.value = applyUpdater(next, sorting.value),
  onColumnVisibilityChange: next => visibility.value = applyUpdater(next, visibility.value)
})

const rows = computed(() => table.getRowModel().rows)
const hideable = computed(() => table.getAllLeafColumns().filter(column => column.getCanHide()))

/** Whether a column is on. The cells are drawn by hand, so they ask. */
const shown = (id: string) => table.getColumn(id)?.getIsVisible() ?? true

/** A header's column, which `SortableHead` drives. Every id asked for is one defined above. */
const column = (id: string) => table.getColumn(id)!

const labels = computed<Record<string, string>>(() => ({
  slug: t('catalog.columns.slug'),
  products: t('catalog.columns.products')
}))
</script>

<template>
  <!-- Over the table's end corner: which columns to show, then the action that adds a row. The
       Columns menu only with a table to act on; the New button always. -->
  <div class="flex items-center justify-end gap-2">
    <ColumnsMenu v-if="categories.length" :columns="hideable" :labels="labels" />
    <Button @click="edit(null)">
      <Plus />
      {{ $t('catalog.newCategory') }}
    </Button>
  </div>

  <TableFrame v-if="categories.length">
    <Table>
      <TableHeader class="sticky-head">
        <TableRow>
          <SortableHead :column="column('name')" :label="$t('catalog.columns.category')" class="ps-4" />
          <SortableHead
            v-if="shown('slug')"
            :column="column('slug')"
            :label="labels.slug!"
            class="hidden md:table-cell"
          />
          <SortableHead
            v-if="shown('products')"
            :column="column('products')"
            :label="labels.products!"
            align="end"
          />
          <TableHead class="w-12 pe-4">
            <span class="sr-only">{{ $t('common.actions') }}</span>
          </TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <TableRow
          v-for="row in rows"
          :key="row.id"
          class="cursor-pointer"
          @click="edit(row.original.id)"
        >
          <TableCell class="ps-4">
            <CatalogNameCell
              :image="row.original.banner"
              :name="row.original.name"
              :description="row.original.description"
              width-class="max-w-40 sm:max-w-56 lg:max-w-md"
              @open="edit(row.original.id)"
            />
          </TableCell>
          <TableCell v-if="shown('slug')" class="hidden md:table-cell">
            <!-- `dir="ltr"`: a slug is a URL segment, declared [NotMultiLingual] for that reason. -->
            <Badge variant="outline" class="text-muted-foreground" dir="ltr">
              {{ row.original.slug }}
            </Badge>
          </TableCell>
          <TableCell v-if="shown('products')" class="text-end tabular-nums">
            {{ row.original.products }}
          </TableCell>
          <TableCell class="pe-4 text-end" @click.stop>
            <CatalogRowActions
              :delete-disabled="row.original.products > 0"
              @edit="edit(row.original.id)"
              @delete="confirming = { id: row.original.id, name: row.original.name }"
            />
          </TableCell>
        </TableRow>
      </TableBody>
    </Table>

    <TablePager :page="1" :page-count="1">
      <span class="tabular-nums">{{ $t('catalog.categories.count', categories.length) }}</span>
    </TablePager>
  </TableFrame>

  <Empty v-else class="border border-dashed">
    <EmptyHeader>
      <EmptyMedia variant="icon">
        <FolderTree />
      </EmptyMedia>
      <EmptyTitle>{{ $t('catalog.categories.empty') }}</EmptyTitle>
      <EmptyDescription>{{ $t('catalog.categories.emptyHint') }}</EmptyDescription>
    </EmptyHeader>
    <EmptyContent>
      <Button size="sm" @click="edit(null)">
        <Plus />
        {{ $t('catalog.newCategory') }}
      </Button>
    </EmptyContent>
  </Empty>

  <CategoryEditor
    v-model:open="editorOpen"
    :category-id="editing"
    :languages="languages"
    @saved="emit('changed')"
  />

  <ConfirmModal
    :open="confirming !== null"
    :title="$t('catalog.confirmDelete.title', { name: confirming?.name ?? '' })"
    :description="$t('catalog.confirmDelete.description')"
    :confirm-label="$t('common.delete')"
    :loading="deleting"
    @update:open="confirming = null"
    @confirm="confirm"
  />
</template>
