<script setup lang="ts">
import type { ColumnVisibilityState, SortingState } from '@tanstack/vue-table'
import { FilterX, FolderTree, Images, Package, Plus, SearchX } from '@lucide/vue'
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
import type { Category, Language, Product } from '~/types'

/**
 * The Products tab: the filters, a page of the listing, its editor, and deleting a row.
 *
 * Products come a page at a time, ordered by when they were added, and the API takes no other
 * order — so a sort here reorders the page on screen, and the footer says "within this page"
 * while it does.
 */
const props = defineProps<{
  categories: Category[]
  languages: Language[]
}>()

const emit = defineEmits<{
  /** A product was written or deleted, which moves the categories' product counts. */
  changed: []
}>()

const { t } = useI18n()
const api = useApi()

const {
  page, searchBox, categoryId, isFiltered, clearFilters,
  products, pageSize, pageCount, range, pending, refresh
} = await useProductList()

// `currencySymbol` for the price: the only catalogue setting a listing does not already carry.
const { data: catalogSettings } = await useCatalogSettings()
const currencySymbol = computed(() => catalogSettings.value?.currencySymbol ?? '')

const categoryName = (id: number) => props.categories.find(c => c.id === id)?.name ?? '—'

const editing = ref<number | null>(null)
const editorOpen = ref(false)

function edit(id: number | null) {
  editing.value = id
  editorOpen.value = true
}

/**
 * `createdId` is set only for a create, and the editor stays open for one: a product cannot hold
 * media until it has a key, so its first image can only be attached from that panel. Switching
 * `editing` onto the new id turns the "new product" panel into an "edit product" one.
 */
async function onSaved(createdId?: number) {
  if (createdId !== undefined) editing.value = createdId

  emit('changed')
  await refresh()
}

const { confirming, deleting, confirm } = useCatalogDelete(
  id => api.products.remove(id),
  async () => {
    emit('changed')
    await refresh()
  })

const features = tableFeatures({
  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
  sortFns: { alphanumeric: sortFn_alphanumeric, basic: sortFn_basic, text: sortFn_text },
  columnVisibilityFeature
})

const helper = createColumnHelper<typeof features, Product>()

const columns = helper.columns([
  helper.accessor('name', { sortFn: 'text', enableHiding: false }),
  helper.accessor('sku', { sortFn: 'alphanumeric' }),
  helper.accessor(product => categoryName(product.categoryId), { id: 'category', sortFn: 'text' }),
  helper.accessor(product => product.images.length, { id: 'images', sortFn: 'basic', sortDescFirst: true }),
  helper.accessor('price', { sortFn: 'basic' }),
  helper.display({ id: 'actions', enableSorting: false, enableHiding: false })
])

const sorting = ref<SortingState>([])

// Remembered per browser, like the Audit table's: how one reader likes the table laid out.
const visibility = useLocalStorage<ColumnVisibilityState>('catalog-products-columns', {})

const table = useTable({
  features,
  columns,
  data: products,
  getRowId: (product: Product) => String(product.id),
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
  sku: t('catalog.columns.sku'),
  category: t('catalog.columns.category'),
  images: t('catalog.columns.images'),
  price: t('catalog.columns.price')
}))
</script>

<template>
  <CatalogProductFilters
    v-model:search="searchBox"
    v-model:category-id="categoryId"
    :categories="categories"
    :filtered="isFiltered"
    :pending="pending"
    @clear="clearFilters"
  >
    <ColumnsMenu :columns="hideable" :labels="labels" />
    <Button @click="edit(null)">
      <Plus />
      {{ $t('catalog.newProduct') }}
    </Button>
  </CatalogProductFilters>

  <TableFrame v-if="products.length">
    <Table>
      <TableHeader class="sticky-head">
        <TableRow>
          <SortableHead :column="column('name')" :label="$t('catalog.columns.product')" class="ps-4" />
          <SortableHead
            v-if="shown('sku')"
            :column="column('sku')"
            :label="labels.sku!"
            class="hidden lg:table-cell"
          />
          <SortableHead
            v-if="shown('category')"
            :column="column('category')"
            :label="labels.category!"
            class="hidden md:table-cell"
          />
          <SortableHead
            v-if="shown('images')"
            :column="column('images')"
            :label="labels.images!"
            align="end"
            class="hidden xl:table-cell"
          />
          <SortableHead
            v-if="shown('price')"
            :column="column('price')"
            :label="labels.price!"
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
              :image="row.original.primary"
              :name="row.original.name"
              :description="row.original.description"
              width-class="max-w-32 sm:max-w-56 lg:max-w-sm"
              @open="edit(row.original.id)"
            />
          </TableCell>
          <TableCell v-if="shown('sku')" class="hidden lg:table-cell">
            <Badge variant="outline" class="text-muted-foreground" dir="ltr">
              {{ row.original.sku }}
            </Badge>
          </TableCell>
          <TableCell v-if="shown('category')" class="hidden md:table-cell">
            <Badge variant="outline" class="text-muted-foreground">
              <FolderTree />
              {{ categoryName(row.original.categoryId) }}
            </Badge>
          </TableCell>
          <TableCell v-if="shown('images')" class="hidden text-end xl:table-cell">
            <span v-if="row.original.images.length" class="inline-flex items-center gap-1.5 tabular-nums text-muted-foreground">
              <Images class="size-3.5" />
              {{ row.original.images.length }}
            </span>
            <span v-else class="text-muted-foreground/60">—</span>
          </TableCell>
          <!-- No `dir` override, unlike the SKU beside it: `formatPrice` asks Intl to place the
               symbol for the reading locale, and forcing LTR would undo "349.00 $" in Arabic. -->
          <TableCell v-if="shown('price')" class="text-end font-medium tabular-nums">
            {{ formatPrice(row.original.price, currencySymbol) }}
          </TableCell>
          <TableCell class="pe-4 text-end" @click.stop>
            <CatalogRowActions
              @edit="edit(row.original.id)"
              @delete="confirming = { id: row.original.id, name: row.original.name }"
            />
          </TableCell>
        </TableRow>
      </TableBody>
    </Table>

    <!-- The page size is not a control: it is `CatalogSettings.PageSize`, so the footer says what
         it is and links to where it is decided. -->
    <TablePager v-model:page="page" :page-count="pageCount">
      <span class="tabular-nums">{{ range }}</span>
      <span v-if="isFiltered" class="text-muted-foreground/70">· {{ $t('common.filtered') }}</span>
      <!-- Only with more than one page: on a single page a sort covers every product. -->
      <span v-if="sorting.length && pageCount > 1" class="flex items-center gap-1.5 text-xs">
        <span aria-hidden="true">·</span>
        {{ $t('catalog.table.sortedThisPage') }}
        <Button
          variant="link"
          size="xs"
          class="h-auto p-0"
          @click="sorting = []"
        >
          {{ $t('catalog.table.resetSort') }}
        </Button>
      </span>

      <template #size>
        <span class="hidden text-muted-foreground sm:inline">
          {{ $t('catalog.table.pageSize', { n: pageSize }) }}
          <NuxtLink to="/settings/catalog" class="font-medium text-foreground underline-offset-4 hover:underline">
            {{ $t('nav.settings') }}
            <!-- Mirrored in Arabic, where a path reads right to left. -->
            <span class="inline-block rtl:-scale-x-100" aria-hidden="true">›</span>
            {{ groupLabel('catalog', 'CatalogSettings') }}
          </NuxtLink>
        </span>
      </template>
    </TablePager>
  </TableFrame>

  <!-- Nothing matched: a different empty from "no products yet", and the way out is the filters
       rather than the New button. -->
  <Empty v-else-if="isFiltered" class="border border-dashed">
    <EmptyHeader>
      <EmptyMedia variant="icon">
        <SearchX />
      </EmptyMedia>
      <EmptyTitle>{{ $t('catalog.filters.emptyTitle') }}</EmptyTitle>
      <EmptyDescription>{{ $t('catalog.filters.emptyDescription') }}</EmptyDescription>
    </EmptyHeader>
    <EmptyContent>
      <Button variant="outline" size="sm" @click="clearFilters">
        <FilterX />
        {{ $t('catalog.filters.clear') }}
      </Button>
    </EmptyContent>
  </Empty>

  <Empty v-else class="border border-dashed">
    <EmptyHeader>
      <EmptyMedia variant="icon">
        <Package />
      </EmptyMedia>
      <EmptyTitle>{{ $t('catalog.products.empty') }}</EmptyTitle>
      <EmptyDescription>{{ $t('catalog.products.emptyHint') }}</EmptyDescription>
    </EmptyHeader>
    <EmptyContent>
      <Button size="sm" @click="edit(null)">
        <Plus />
        {{ $t('catalog.newProduct') }}
      </Button>
    </EmptyContent>
  </Empty>

  <ProductEditor
    v-model:open="editorOpen"
    :product-id="editing"
    :languages="languages"
    :categories="categories"
    @saved="onSaved"
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
