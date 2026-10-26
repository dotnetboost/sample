<script setup lang="ts">
import { EllipsisVertical, FolderTree, ImageOff, Package, Pencil, Plus, Trash2 } from '@lucide/vue'

const { t } = useI18n()

useHead({ title: () => t('nav.documentTitle', { page: t('nav.catalog') }) })

const api = useApi()
const { notifySuccess, notifyFailure } = useNotify()
const language = useLanguage()

const tab = ref('categories')

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

const { data: productPage, refresh: refreshProducts } = await useAsyncData(
  'catalog-products',
  (_nuxtApp, { signal }) => api.products.list({ page: page.value }, { signal }),
  { watch: [page] })

// `currencySymbol` for the price: the only catalogue setting a listing does not already carry.
const { data: catalogSettings } = await useCatalogSettings()

const products = computed(() => productPage.value?.items ?? [])
const productTotal = computed(() => productPage.value?.total ?? 0)
const pageSize = computed(() => productPage.value?.take ?? 0)
const pageCount = computed(() =>
  pageSize.value ? Math.ceil(productTotal.value / pageSize.value) : 1)

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
      <Card v-if="categories?.length" class="py-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead class="ps-4">
                {{ $t('catalog.columns.category') }}
              </TableHead>
              <TableHead>{{ $t('catalog.columns.slug') }}</TableHead>
              <TableHead>{{ $t('catalog.columns.products') }}</TableHead>
              <TableHead class="w-12 pe-4">
                <span class="sr-only">{{ $t('common.actions') }}</span>
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow v-for="category in categories" :key="category.id">
              <TableCell class="ps-4">
                <div class="flex min-w-0 items-center gap-3">
                  <div class="flex size-11 shrink-0 items-center justify-center overflow-hidden rounded-md border bg-muted/40">
                    <img
                      v-if="category.banner"
                      :src="contentUrl(category.banner.id)"
                      :alt="category.banner.altText ?? category.name"
                      class="size-full object-cover"
                    >
                    <ImageOff v-else class="size-4 text-muted-foreground" />
                  </div>
                  <div class="min-w-0 max-w-56 lg:max-w-md">
                    <p class="truncate font-medium">
                      {{ category.name }}
                    </p>
                    <p class="truncate text-muted-foreground">
                      {{ category.description }}
                    </p>
                  </div>
                </div>
              </TableCell>
              <TableCell>
                <!-- `dir="ltr"`: a slug is a URL segment, declared [NotMultiLingual] for that reason. -->
                <Badge variant="outline" class="font-mono" dir="ltr">
                  {{ category.slug }}
                </Badge>
              </TableCell>
              <TableCell>
                <Badge variant="secondary">
                  {{ $t('catalog.productCount', category.products) }}
                </Badge>
              </TableCell>
              <TableCell class="pe-4 text-end">
                <DropdownMenu>
                  <DropdownMenuTrigger as-child>
                    <Button variant="ghost" size="icon-sm" :aria-label="$t('common.actions')">
                      <EllipsisVertical />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem @select="editCategory(category.id)">
                      <Pencil />
                      {{ $t('common.edit') }}
                    </DropdownMenuItem>
                    <DropdownMenuSeparator />
                    <DropdownMenuItem
                      variant="destructive"
                      :disabled="category.products > 0"
                      @select="confirming = { kind: 'category', id: category.id, name: category.name }"
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
      </Card>

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
      <Card v-if="products.length" class="py-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead class="ps-4">
                {{ $t('catalog.columns.product') }}
              </TableHead>
              <TableHead>{{ $t('catalog.columns.sku') }}</TableHead>
              <TableHead>{{ $t('catalog.columns.price') }}</TableHead>
              <TableHead>{{ $t('catalog.columns.category') }}</TableHead>
              <TableHead>{{ $t('catalog.columns.images') }}</TableHead>
              <TableHead class="w-12 pe-4">
                <span class="sr-only">{{ $t('common.actions') }}</span>
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow v-for="product in products" :key="product.id">
              <TableCell class="ps-4">
                <div class="flex min-w-0 items-center gap-3">
                  <div class="flex size-11 shrink-0 items-center justify-center overflow-hidden rounded-md border bg-muted/40">
                    <img
                      v-if="product.primary"
                      :src="contentUrl(product.primary.id)"
                      :alt="product.primary.altText ?? product.name"
                      class="size-full object-cover"
                    >
                    <ImageOff v-else class="size-4 text-muted-foreground" />
                  </div>
                  <div class="min-w-0 max-w-56 lg:max-w-md">
                    <p class="truncate font-medium">
                      {{ product.name }}
                    </p>
                    <p class="truncate text-muted-foreground">
                      {{ product.description }}
                    </p>
                  </div>
                </div>
              </TableCell>
              <TableCell>
                <Badge variant="outline" class="font-mono" dir="ltr">
                  {{ product.sku }}
                </Badge>
              </TableCell>
              <!-- No `dir` override, unlike the SKU beside it: `formatPrice` asks Intl to place the
                   symbol for the reading locale and Intl marks the result's direction itself, so
                   Arabic gets "349.00 $" and forcing LTR here would undo that. A SKU is a Latin code
                   in every language and does need the override. -->
              <TableCell class="tabular-nums">
                {{ formatPrice(product.price, currencySymbol) }}
              </TableCell>
              <TableCell>
                <Badge variant="secondary">
                  {{ categoryName(product.categoryId) }}
                </Badge>
              </TableCell>
              <TableCell class="text-muted-foreground">
                {{ product.images.length ? $t('catalog.imageCount', product.images.length) : '—' }}
              </TableCell>
              <TableCell class="pe-4 text-end">
                <DropdownMenu>
                  <DropdownMenuTrigger as-child>
                    <Button variant="ghost" size="icon-sm" :aria-label="$t('common.actions')">
                      <EllipsisVertical />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem @select="editProduct(product.id)">
                      <Pencil />
                      {{ $t('common.edit') }}
                    </DropdownMenuItem>
                    <DropdownMenuSeparator />
                    <DropdownMenuItem
                      variant="destructive"
                      @select="confirming = { kind: 'product', id: product.id, name: product.name }"
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
      </Card>

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

      <div v-if="productTotal" class="flex flex-wrap items-center justify-between gap-3">
        <p class="text-sm text-muted-foreground">
          {{ range }}
        </p>

        <PagerControls
          v-if="pageCount > 1"
          v-model:page="page"
          :total="productTotal"
          :items-per-page="pageSize"
        />
      </div>
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
