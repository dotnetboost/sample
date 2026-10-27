<script setup lang="ts">
import type { ColumnVisibilityState, SortingState, Updater } from '@tanstack/vue-table'
import { EllipsisVertical, FilterX, FolderTree, ImageOff, Images, Package, Pencil, Plus, Search, SearchX, Trash2, X } from '@lucide/vue'
import { watchDebounced } from '@vueuse/core'
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
import type { Category, Product } from '~/types'

const { t } = useI18n()

useHead({ title: () => t('nav.documentTitle', { page: t('nav.catalog') }) })

const api = useApi()
const { notifySuccess, notifyFailure } = useNotify()
const language = useLanguage()

/**
 * Which tab is open, kept in the address as `?tab=products` — so the sidebar can link straight to
 * either tab, and a reload or a shared link lands on the one that was open. Categories is the
 * default and takes no parameter, so `/catalog` stays the plain address of the page.
 *
 * `replace` rather than `push`: switching tabs is looking at the other half of one page, and a
 * back button that walked through every tab switch would be a back button that does not leave.
 */
const route = useRoute()
const router = useRouter()

const tab = computed<'categories' | 'products'>({
  get: () => route.query.tab === 'products' ? 'products' : 'categories',
  set: value => router.replace({ query: { ...route.query, tab: value === 'products' ? 'products' : undefined } })
})

// The one shared language list: a language added on the Languages page has to reach the
// translation editors below without a reload.
const { languages } = useLanguageList()
const { data: categories, refresh: refreshCategories } = await useAsyncData(
  'catalog-categories', (_nuxtApp, { signal }) => api.categories.list({ signal }))

/**
 * Which page of products is on screen.
 *
 * How MANY is not here: `api.products.list()` is called with no page size, so the API answers with
 * `CatalogSettings.PageSize` and tells us what it used in `take`. Deciding it here as well would
 * be a second answer to a question the settings group already answers, and the two would disagree
 * the moment someone edited the setting.
 */
const page = ref(1)

/**
 * The products filter bar: a search box and a category. Both are parameters on
 * `GET /api/products`, never a filter over the rows already on screen — that would only ever
 * search the page you are looking at, and say nothing about the rest.
 *
 * The search is matched by the API in the reading language (see `ListProducts`), so typing the
 * Arabic name of a product finds it while the dashboard is in Arabic. The box is bound to its own
 * ref and debounced into the question: bound straight through, every keystroke would be a request.
 *
 * A change to either goes back to page 1, because the old page number belongs to the old
 * question — page 3 of everything can be past the end of one category.
 */
const searchBox = ref('')
const search = ref('')
const categoryFilter = ref<number | undefined>()

watchDebounced(searchBox, (value) => {
  search.value = value.trim()
}, { debounce: 300 })

watch([search, categoryFilter], () => {
  page.value = 1
})

const isFiltered = computed(() => !!search.value || categoryFilter.value !== undefined)

function clearFilters() {
  searchBox.value = ''
  search.value = ''
  categoryFilter.value = undefined
}

const { data: productPage, refresh: refreshProducts, pending: productsPending } = await useAsyncData(
  'catalog-products',
  (_nuxtApp, { signal }) => api.products.list({
    page: page.value,
    search: search.value,
    categoryId: categoryFilter.value
  }, { signal }),
  { watch: [page, search, categoryFilter] })

// `currencySymbol` for the price: the only catalogue setting a listing does not already carry.
const { data: catalogSettings } = await useCatalogSettings()

// One empty array for "nothing yet", not a fresh `[]` per read: the products table below takes
// this as its data, and a new array each time is a new dataset each time.
const noProducts: Product[] = []
const noCategories: Category[] = []

const products = computed(() => productPage.value?.items ?? noProducts)
const categoryRows = computed(() => categories.value ?? noCategories)
const productTotal = computed(() => productPage.value?.total ?? 0)
const pageSize = computed(() => productPage.value?.take ?? 0)
// Never below 1: a filter that matches nothing is one empty page, not none, and a count of zero
// would have the clamp below send the pager to page 0.
const pageCount = computed(() =>
  pageSize.value ? Math.max(1, Math.ceil(productTotal.value / pageSize.value)) : 1)

const currencySymbol = computed(() => catalogSettings.value?.currencySymbol ?? '')

const range = computed(() => productTotal.value
  ? t('catalog.products.range', {
      from: (productPage.value?.skip ?? 0) + 1,
      to: (productPage.value?.skip ?? 0) + products.value.length,
      total: productTotal.value
    })
  : '')

// Re-read when the header language changes: these are the same rows, translated.
watch(language, async () => {
  await Promise.all([refreshCategories(), refreshProducts()])
})

// A delete can empty the last page, which would otherwise leave a pager pointing past the end at
// an empty list. Mirrors what useMediaLibrary does for the same reason.
watch([productTotal, pageSize], () => {
  if (page.value > pageCount.value) {
    page.value = pageCount.value
  }
})

const editingCategory = ref<number | null>(null)
const categoryOpen = ref(false)
const editingProduct = ref<number | null>(null)
const productOpen = ref(false)

const confirming = ref<{ kind: 'category' | 'product', id: number, name: string } | null>(null)
const deleting = ref(false)

function editCategory(id: number | null) {
  editingCategory.value = id
  categoryOpen.value = true
}

function editProduct(id: number | null) {
  editingProduct.value = id
  productOpen.value = true
}

/**
 * A product was written. `createdId` is set only for a CREATE, and the editor stays open for one:
 * a product cannot hold media until it has a key, so its first image can only be attached from
 * that panel, and closing would send you back to the list to find the row and reopen it.
 * Switching `editingProduct` onto the new id turns the open "new product" panel into an "edit
 * product" one, and the media sections that were showing "save first" become usable.
 */
async function onProductSaved(createdId?: number) {
  if (createdId !== undefined) editingProduct.value = createdId

  await Promise.all([refreshProducts(), refreshCategories()])
}

async function confirmDelete() {
  if (!confirming.value) return

  deleting.value = true
  try {
    let message: string

    if (confirming.value.kind === 'category') {
      ({ message } = await api.categories.remove(confirming.value.id))
      await refreshCategories()
    } else {
      ({ message } = await api.products.remove(confirming.value.id))
      await Promise.all([refreshProducts(), refreshCategories()])
    }

    // "Deleted" on its own left the reader to work out which of two rows went. The API says which,
    // by slug or SKU, in the language the list is being read in — the same place the refusal in the
    // catch block comes from.
    notifySuccess(t('catalog.toast.deleted'), message)
    confirming.value = null
  } catch (error) {
    // The refusal that matters here is "this category still has products", which the API sends as
    // problem details with a localized `detail`. Reading `data.message` was left over from the
    // ad-hoc shape that endpoint used to answer with, and matched nothing any more: every delete
    // that was refused fell through to `String(error)` and showed a raw FetchError.
    notifyFailure(t('catalog.toast.deleteFailed'), error)
  } finally {
    deleting.value = false
  }
}

const categoryName = (id: number) => categories.value?.find(c => c.id === id)?.name ?? '—'

/**
 * Both tables, on TanStack Table — as the Audit table is, for the same two things only: the order
 * of the rows on screen, and which columns are shown. The markup stays this file's.
 *
 * **What a sort covers differs between the two, and says so.** The categories list is the whole
 * list — the API does not page it — so sorting it sorts every category. Products come a page at a
 * time, ordered by when they were added, and the API takes no other order; sorting them reorders
 * the page on screen, and the footer says "within this page" while it does.
 *
 * Unsorted is allowed here, unlike on Audit: the API's order is "as added", which is a real order
 * worth getting back to, and a third click on a header returns to it.
 */
const features = tableFeatures({
  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
  sortFns: { alphanumeric: sortFn_alphanumeric, basic: sortFn_basic, text: sortFn_text },
  columnVisibilityFeature
})

/** TanStack hands state changes over as a value or as a function of the old one. */
function resolve<T>(next: Updater<T>, current: T): T {
  return typeof next === 'function' ? (next as (old: T) => T)(current) : next
}

const categoryHelper = createColumnHelper<typeof features, Category>()

const categoryColumns = categoryHelper.columns([
  categoryHelper.accessor('name', { sortFn: 'text', enableHiding: false }),
  categoryHelper.accessor('slug', { sortFn: 'alphanumeric' }),
  categoryHelper.accessor('products', { sortFn: 'basic', sortDescFirst: true }),
  categoryHelper.display({ id: 'actions', enableSorting: false, enableHiding: false })
])

const productHelper = createColumnHelper<typeof features, Product>()

const productColumns = productHelper.columns([
  productHelper.accessor('name', { sortFn: 'text', enableHiding: false }),
  productHelper.accessor('sku', { sortFn: 'alphanumeric' }),
  productHelper.accessor(product => categoryName(product.categoryId), { id: 'category', sortFn: 'text' }),
  productHelper.accessor(product => product.images.length, { id: 'images', sortFn: 'basic', sortDescFirst: true }),
  productHelper.accessor('price', { sortFn: 'basic' }),
  productHelper.display({ id: 'actions', enableSorting: false, enableHiding: false })
])

const categorySorting = ref<SortingState>([])
const productSorting = ref<SortingState>([])

// Remembered per browser, like the Audit table's: how one reader likes the table laid out.
const categoryVisibility = useLocalStorage<ColumnVisibilityState>('catalog-categories-columns', {})
const productVisibility = useLocalStorage<ColumnVisibilityState>('catalog-products-columns', {})

const categoryTable = useTable({
  features,
  columns: categoryColumns,
  data: categoryRows,
  getRowId: (category: Category) => String(category.id),
  state: computed(() => ({ sorting: categorySorting.value, columnVisibility: categoryVisibility.value })),
  onSortingChange: next => categorySorting.value = resolve(next, categorySorting.value),
  onColumnVisibilityChange: next => categoryVisibility.value = resolve(next, categoryVisibility.value)
})

const productTable = useTable({
  features,
  columns: productColumns,
  data: products,
  getRowId: (product: Product) => String(product.id),
  state: computed(() => ({ sorting: productSorting.value, columnVisibility: productVisibility.value })),
  onSortingChange: next => productSorting.value = resolve(next, productSorting.value),
  onColumnVisibilityChange: next => productVisibility.value = resolve(next, productVisibility.value)
})

const categoryRowsSorted = computed(() => categoryTable.getRowModel().rows)
const productRowsSorted = computed(() => productTable.getRowModel().rows)

const columnLabels = computed<Record<string, string>>(() => ({
  slug: t('catalog.columns.slug'),
  products: t('catalog.columns.products'),
  sku: t('catalog.columns.sku'),
  category: t('catalog.columns.category'),
  images: t('catalog.columns.images'),
  price: t('catalog.columns.price')
}))

/** What each table's Columns menu offers: the name and the menu column always stay. */
const hideableCategoryColumns = computed(() =>
  categoryTable.getAllLeafColumns().filter(column => column.getCanHide()))
const hideableProductColumns = computed(() =>
  productTable.getAllLeafColumns().filter(column => column.getCanHide()))

/** Whether a column of the open table is on. The cells are drawn by hand, so they ask. */
const shownCategory = (id: string) => categoryTable.getColumn(id)?.getIsVisible() ?? true
const shownProduct = (id: string) => productTable.getColumn(id)?.getIsVisible() ?? true

/** A header's column, which `SortableHead` drives. Every id asked for is one defined above. */
const categoryColumn = (id: string) => categoryTable.getColumn(id)!
const productColumn = (id: string) => productTable.getColumn(id)!
</script>

<template>
  <DashboardPage :title="$t('nav.catalog')">
    <template #toolbar>
      <Tabs v-model="tab">
        <TabsList>
          <TabsTrigger value="categories">
            <FolderTree />
            {{ $t('catalog.tabs.categories') }}
          </TabsTrigger>
          <TabsTrigger value="products">
            <Package />
            {{ $t('catalog.tabs.products') }}
          </TabsTrigger>
        </TabsList>
      </Tabs>

      <Button
        v-if="tab === 'categories'"
        size="sm"
        class="ms-auto"
        @click="editCategory(null)"
      >
        <Plus />
        {{ $t('catalog.newCategory') }}
      </Button>
      <Button
        v-else
        size="sm"
        class="ms-auto"
        @click="editProduct(null)"
      >
        <Plus />
        {{ $t('catalog.newProduct') }}
      </Button>
    </template>

    <SectionIntro
      :title="tab === 'categories' ? $t('catalog.categories.title') : $t('catalog.products.title')"
      :description="tab === 'categories'
        ? $t('catalog.categories.description')
        : $t('catalog.products.description')"
    />

    <!-- ── Categories ───────────────────────────────────────────────── -->
    <template v-if="tab === 'categories'">
      <!--
        A row opens its editor wherever it is clicked; the name is also a real button, so the same
        thing is reachable from the keyboard. The menu cell stops the click, or choosing Delete
        would open the editor behind the confirmation.

        `dir="auto"` on the name and description: a row with no Arabic translation comes back in
        the default language's English, and read right-to-left it truncates from the wrong end.
      -->
      <!-- Over the table's end corner, as on Products and Audit, where it ends the filter bar. -->
      <div v-if="categories?.length" class="flex justify-end">
        <ColumnsMenu :columns="hideableCategoryColumns" :labels="columnLabels" />
      </div>

      <TableFrame v-if="categories?.length">
        <Table>
          <TableHeader class="sticky-head">
            <TableRow>
              <SortableHead :column="categoryColumn('name')" :label="$t('catalog.columns.category')" class="ps-4" />
              <SortableHead
                v-if="shownCategory('slug')"
                :column="categoryColumn('slug')"
                :label="columnLabels.slug!"
                class="hidden md:table-cell"
              />
              <SortableHead
                v-if="shownCategory('products')"
                :column="categoryColumn('products')"
                :label="columnLabels.products!"
                align="end"
              />
              <TableHead class="w-12 pe-4">
                <span class="sr-only">{{ $t('common.actions') }}</span>
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow
              v-for="row in categoryRowsSorted"
              :key="row.id"
              class="cursor-pointer"
              @click="editCategory(row.original.id)"
            >
              <TableCell class="ps-4">
                <div class="flex min-w-0 items-center gap-3">
                  <div class="flex size-10 shrink-0 items-center justify-center overflow-hidden rounded-md border bg-muted/40">
                    <img
                      v-if="row.original.banner"
                      :src="contentUrl(row.original.banner.id)"
                      :alt="row.original.banner.altText ?? row.original.name"
                      class="size-full object-cover"
                    >
                    <ImageOff v-else class="size-4 text-muted-foreground" />
                  </div>
                  <div class="min-w-0 max-w-40 sm:max-w-56 lg:max-w-md">
                    <button
                      type="button"
                      dir="auto"
                      class="block max-w-full truncate text-start font-medium outline-none hover:underline focus-visible:underline"
                      @click.stop="editCategory(row.original.id)"
                    >
                      {{ row.original.name }}
                    </button>
                    <p dir="auto" class="truncate text-muted-foreground">
                      {{ row.original.description }}
                    </p>
                  </div>
                </div>
              </TableCell>
              <TableCell v-if="shownCategory('slug')" class="hidden md:table-cell">
                <!-- `dir="ltr"`: a slug is a URL segment, declared [NotMultiLingual] for that reason. -->
                <Badge variant="outline" class="font-mono text-muted-foreground" dir="ltr">
                  {{ row.original.slug }}
                </Badge>
              </TableCell>
              <TableCell v-if="shownCategory('products')" class="text-end tabular-nums">
                {{ row.original.products }}
              </TableCell>
              <TableCell class="pe-4 text-end" @click.stop>
                <DropdownMenu>
                  <DropdownMenuTrigger as-child>
                    <Button variant="ghost" size="icon-sm" :aria-label="$t('common.actions')">
                      <EllipsisVertical />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem @select="editCategory(row.original.id)">
                      <Pencil />
                      {{ $t('common.edit') }}
                    </DropdownMenuItem>
                    <DropdownMenuSeparator />
                    <DropdownMenuItem
                      variant="destructive"
                      :disabled="row.original.products > 0"
                      @select="confirming = { kind: 'category', id: row.original.id, name: row.original.name }"
                    >
                      <Trash2 />
                      {{ $t('common.delete') }}
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>

        <!-- No pages: the API answers with every category, so a sort here sorts all of them. -->
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
          <Button size="sm" @click="editCategory(null)">
            <Plus />
            {{ $t('catalog.newCategory') }}
          </Button>
        </EmptyContent>
      </Empty>
    </template>

    <!-- ── Products ─────────────────────────────────────────────────── -->
    <template v-else>
      <!-- `dir="auto"` on the box: what is typed may be Arabic or a Latin SKU, either way round. -->
      <div class="flex flex-col gap-2 sm:flex-row sm:items-center">
        <InputGroup class="w-full sm:max-w-sm">
          <InputGroupAddon>
            <Spinner v-if="productsPending && searchBox" />
            <Search v-else />
          </InputGroupAddon>
          <InputGroupInput
            v-model="searchBox"
            dir="auto"
            :placeholder="$t('catalog.filters.search')"
            :aria-label="$t('catalog.filters.search')"
          />
          <InputGroupAddon v-if="searchBox" align="inline-end">
            <InputGroupButton size="icon-xs" :aria-label="$t('catalog.filters.clearSearch')" @click="searchBox = ''">
              <X />
            </InputGroupButton>
          </InputGroupAddon>
        </InputGroup>

        <!-- "All categories" is a sentinel rather than an empty value, which reka-ui's select
             refuses because it reserves that for clearing the selection. -->
        <Select
          :model-value="categoryFilter ?? 'all'"
          @update:model-value="categoryFilter = $event === 'all' ? undefined : Number($event)"
        >
          <SelectTrigger class="w-full sm:w-52" :aria-label="$t('catalog.filters.category')">
            <FolderTree class="text-muted-foreground" />
            <!-- The name by itself: left to reka-ui, the trigger repeats the whole option, the
                 product count beside the name included. -->
            <SelectValue>
              <span dir="auto">{{ categoryFilter === undefined
                ? $t('catalog.filters.allCategories')
                : categoryName(categoryFilter) }}</span>
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">
              {{ $t('catalog.filters.allCategories') }}
            </SelectItem>
            <SelectSeparator />
            <SelectItem
              v-for="category in categories ?? []"
              :key="category.id"
              :value="category.id"
              :text-value="category.name"
            >
              <span dir="auto">{{ category.name }}</span>
              <span class="ms-auto ps-3 text-xs tabular-nums text-muted-foreground">{{ category.products }}</span>
            </SelectItem>
          </SelectContent>
        </Select>

        <Button
          v-if="isFiltered"
          variant="ghost"
          class="self-start sm:self-auto"
          @click="clearFilters"
        >
          <FilterX />
          {{ $t('common.clear') }}
        </Button>

        <div class="flex justify-end sm:ms-auto">
          <ColumnsMenu :columns="hideableProductColumns" :labels="columnLabels" />
        </div>
      </div>

      <TableFrame v-if="products.length">
        <Table>
          <TableHeader class="sticky-head">
            <TableRow>
              <SortableHead :column="productColumn('name')" :label="$t('catalog.columns.product')" class="ps-4" />
              <SortableHead
                v-if="shownProduct('sku')"
                :column="productColumn('sku')"
                :label="columnLabels.sku!"
                class="hidden lg:table-cell"
              />
              <SortableHead
                v-if="shownProduct('category')"
                :column="productColumn('category')"
                :label="columnLabels.category!"
                class="hidden md:table-cell"
              />
              <SortableHead
                v-if="shownProduct('images')"
                :column="productColumn('images')"
                :label="columnLabels.images!"
                align="end"
                class="hidden xl:table-cell"
              />
              <SortableHead
                v-if="shownProduct('price')"
                :column="productColumn('price')"
                :label="columnLabels.price!"
                align="end"
              />
              <TableHead class="w-12 pe-4">
                <span class="sr-only">{{ $t('common.actions') }}</span>
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow
              v-for="row in productRowsSorted"
              :key="row.id"
              class="cursor-pointer"
              @click="editProduct(row.original.id)"
            >
              <TableCell class="ps-4">
                <div class="flex min-w-0 items-center gap-3">
                  <div class="flex size-10 shrink-0 items-center justify-center overflow-hidden rounded-md border bg-muted/40">
                    <img
                      v-if="row.original.primary"
                      :src="contentUrl(row.original.primary.id)"
                      :alt="row.original.primary.altText ?? row.original.name"
                      class="size-full object-cover"
                    >
                    <ImageOff v-else class="size-4 text-muted-foreground" />
                  </div>
                  <div class="min-w-0 max-w-32 sm:max-w-56 lg:max-w-sm">
                    <button
                      type="button"
                      dir="auto"
                      class="block max-w-full truncate text-start font-medium outline-none hover:underline focus-visible:underline"
                      @click.stop="editProduct(row.original.id)"
                    >
                      {{ row.original.name }}
                    </button>
                    <p dir="auto" class="truncate text-muted-foreground">
                      {{ row.original.description }}
                    </p>
                  </div>
                </div>
              </TableCell>
              <TableCell v-if="shownProduct('sku')" class="hidden lg:table-cell">
                <Badge variant="outline" class="font-mono text-muted-foreground" dir="ltr">
                  {{ row.original.sku }}
                </Badge>
              </TableCell>
              <TableCell v-if="shownProduct('category')" class="hidden md:table-cell">
                <Badge variant="outline" class="text-muted-foreground">
                  <FolderTree />
                  {{ categoryName(row.original.categoryId) }}
                </Badge>
              </TableCell>
              <TableCell v-if="shownProduct('images')" class="hidden text-end xl:table-cell">
                <span v-if="row.original.images.length" class="inline-flex items-center gap-1.5 tabular-nums text-muted-foreground">
                  <Images class="size-3.5" />
                  {{ row.original.images.length }}
                </span>
                <span v-else class="text-muted-foreground/60">—</span>
              </TableCell>
              <!-- No `dir` override, unlike the SKU beside it: `formatPrice` asks Intl to place the
                   symbol for the reading locale and Intl marks the result's direction itself, so
                   Arabic gets "349.00 $" and forcing LTR here would undo that. A SKU is a Latin code
                   in every language and does need the override. -->
              <TableCell v-if="shownProduct('price')" class="text-end font-medium tabular-nums">
                {{ formatPrice(row.original.price, currencySymbol) }}
              </TableCell>
              <TableCell class="pe-4 text-end" @click.stop>
                <DropdownMenu>
                  <DropdownMenuTrigger as-child>
                    <Button variant="ghost" size="icon-sm" :aria-label="$t('common.actions')">
                      <EllipsisVertical />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem @select="editProduct(row.original.id)">
                      <Pencil />
                      {{ $t('common.edit') }}
                    </DropdownMenuItem>
                    <DropdownMenuSeparator />
                    <DropdownMenuItem
                      variant="destructive"
                      @select="confirming = { kind: 'product', id: row.original.id, name: row.original.name }"
                    >
                      <Trash2 />
                      {{ $t('common.delete') }}
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>

        <!--
          The page size is not offered as a control: it is `CatalogSettings.PageSize`, a setting the
          whole application reads, and a second answer here would disagree with it the moment it
          changed. So the footer says what it is and links to where it is decided.
        -->
        <TablePager v-model:page="page" :page-count="pageCount">
          <span class="tabular-nums">{{ range }}</span>
          <span v-if="isFiltered" class="text-muted-foreground/70">· {{ $t('common.filtered') }}</span>
          <!-- Only with more than one page: on a single page a sort covers every product, and
               "within this page" would be a caveat about nothing. -->
          <span v-if="productSorting.length && pageCount > 1" class="flex items-center gap-1.5 text-xs">
            <span aria-hidden="true">·</span>
            {{ $t('catalog.table.sortedThisPage') }}
            <Button
              variant="link"
              size="xs"
              class="h-auto p-0"
              @click="productSorting = []"
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

      <!-- Nothing matched: a different empty from "no products yet", and the way out is the
           filters rather than the New button. -->
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
          <Button size="sm" @click="editProduct(null)">
            <Plus />
            {{ $t('catalog.newProduct') }}
          </Button>
        </EmptyContent>
      </Empty>
    </template>

    <CategoryEditor
      v-model:open="categoryOpen"
      :category-id="editingCategory"
      :languages="languages ?? []"
      @saved="refreshCategories"
    />

    <ProductEditor
      v-model:open="productOpen"
      :product-id="editingProduct"
      :languages="languages ?? []"
      :categories="categories ?? []"
      @saved="onProductSaved"
    />

    <ConfirmModal
      :open="confirming !== null"
      :title="$t('catalog.confirmDelete.title', { name: confirming?.name ?? '' })"
      :description="$t('catalog.confirmDelete.description')"
      :confirm-label="$t('common.delete')"
      :loading="deleting"
      @update:open="confirming = null"
      @confirm="confirmDelete"
    />
  </DashboardPage>
</template>
