<script setup lang="ts">
import type { ColumnVisibilityState, SortingState, Updater } from '@tanstack/vue-table'
import { CircleCheck, CircleOff, Database, EllipsisVertical, Eraser, Languages, Pencil, PilcrowLeft, PilcrowRight, PlugZap, Plus, RefreshCw, Star, Trash2 } from '@lucide/vue'
import {
  columnVisibilityFeature,
  createColumnHelper,
  createSortedRowModel,
  rowSortingFeature,
  sortFn_text,
  tableFeatures,
  useTable
} from '@tanstack/vue-table'
import type { Language } from '~/types'

const { t } = useI18n()

useHead({ title: () => t('nav.documentTitle', { page: t('nav.languages') }) })

const { languages, pending, loadError, busy, refresh, isBase, update, setDefault, remove, purge } = useLanguages()

const formOpen = ref(false)
const editing = ref<Language>()

/** The destructive action awaiting confirmation. */
const confirming = ref<{ kind: 'delete' | 'purge', language: Language } | null>(null)

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

/**
 * Which languages are shown: all of them, the active ones, or the ones switched off — tabs over
 * the table, as on Audit.
 *
 * **Narrowed here, not asked of the API**, unlike every other filter in this dashboard, and for a
 * reason that matters: `GET api/languages` answers with the whole list in one go. There are no
 * pages, so the rows on screen are all the rows there are, and filtering them cannot leave
 * anything out — which is the one condition under which a filter in the browser is honest. The
 * counts on the tabs come from the same list.
 */
type ActiveFilter = 'all' | 'active' | 'inactive'

const activeFilter = ref<ActiveFilter>('all')

const counts = computed(() => ({
  all: languages.value.length,
  active: languages.value.filter(l => l.isActive).length,
  inactive: languages.value.filter(l => !l.isActive).length
}))

const shownLanguages = computed(() => activeFilter.value === 'all'
  ? languages.value
  : languages.value.filter(l => l.isActive === (activeFilter.value === 'active')))

/**
 * The table, on TanStack Table as the others are, for two things: the order of the rows and which
 * columns are shown. Code and name sort over the whole list, since there are no pages; a third
 * click returns to the API's order, the default first.
 */
const features = tableFeatures({
  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
  sortFns: { text: sortFn_text },
  columnVisibilityFeature
})

const helper = createColumnHelper<typeof features, Language>()

const columns = helper.columns([
  helper.accessor('code', { sortFn: 'text', enableHiding: false }),
  helper.accessor('name', { sortFn: 'text', enableHiding: false }),
  helper.accessor('direction', { enableSorting: false }),
  helper.display({ id: 'role', enableSorting: false }),
  helper.accessor('isActive', { id: 'active', enableSorting: false }),
  helper.display({ id: 'actions', enableSorting: false, enableHiding: false })
])

function resolve<T>(next: Updater<T>, current: T): T {
  return typeof next === 'function' ? (next as (old: T) => T)(current) : next
}

const sorting = ref<SortingState>([])
// Remembered per browser, as the other tables' are.
const columnVisibility = useLocalStorage<ColumnVisibilityState>('languages-table-columns', {})

const table = useTable({
  features,
  columns,
  data: shownLanguages,
  getRowId: (language: Language) => language.code,
  state: computed(() => ({ sorting: sorting.value, columnVisibility: columnVisibility.value })),
  onSortingChange: next => sorting.value = resolve(next, sorting.value),
  onColumnVisibilityChange: next => columnVisibility.value = resolve(next, columnVisibility.value)
})

const rows = computed(() => table.getRowModel().rows)

const shown = (id: string) => table.getColumn(id)?.getIsVisible() ?? true
const column = (id: string) => table.getColumn(id)!

const hideable = computed(() => table.getAllLeafColumns().filter(c => c.getCanHide()))

const columnLabels = computed<Record<string, string>>(() => ({
  code: t('languages.columns.code'),
  name: t('languages.columns.name'),
  direction: t('languages.columns.direction'),
  role: t('languages.columns.role'),
  active: t('languages.columns.active')
}))

/** How many columns a full-width cell spans: whatever is on. */
const span = computed(() => table.getVisibleLeafColumns().length)

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
</script>

<template>
  <DashboardPage :title="$t('nav.languages')">
    <template #actions>
      <Button
        variant="ghost"
        size="icon-sm"
        :disabled="pending"
        :aria-label="$t('languages.reload')"
        @click="() => refresh()"
      >
        <RefreshCw :class="{ 'animate-spin': pending }" />
      </Button>
    </template>

    <SectionIntro :title="$t('languages.title')" :description="$t('languages.card.description')">
      <Badge variant="outline" dir="ltr">
        api/languages
      </Badge>
    </SectionIntro>

    <Alert v-if="loadError" variant="destructive">
      <PlugZap />
      <AlertTitle>{{ $t('languages.loadError') }}</AlertTitle>
      <AlertDescription>{{ loadError }}</AlertDescription>
      <AlertAction>
        <Button variant="outline" size="sm" @click="() => refresh()">
          {{ $t('common.tryAgain') }}
        </Button>
      </AlertAction>
    </Alert>

    <template v-else>
      <!-- The bar over the table, as on Audit: which languages on the start side, with how many
           each tab holds; which columns and Add language on the end. -->
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Tabs v-model="activeFilter">
          <TabsList :aria-label="$t('languages.filters.label')">
            <TabsTrigger value="all">
              {{ $t('languages.filters.all') }}
              <span class="tabular-nums text-muted-foreground">{{ counts.all }}</span>
            </TabsTrigger>
            <TabsTrigger value="active">
              <CircleCheck class="text-muted-foreground" />
              {{ $t('languages.filters.active') }}
              <span class="tabular-nums text-muted-foreground">{{ counts.active }}</span>
            </TabsTrigger>
            <TabsTrigger value="inactive">
              <CircleOff class="text-muted-foreground" />
              {{ $t('languages.filters.inactive') }}
              <span class="tabular-nums text-muted-foreground">{{ counts.inactive }}</span>
            </TabsTrigger>
          </TabsList>
        </Tabs>

        <!-- Which columns, then the one action that adds a row — the pair that ends the Catalog
             bars too. -->
        <div class="flex items-center gap-2">
          <ColumnsMenu :columns="hideable" :labels="columnLabels" />
          <Button @click="add">
            <Plus />
            {{ $t('languages.add') }}
          </Button>
        </div>
      </div>

      <!-- A row opens the language's editor; the switch and the menu stop the click, because each
           is a control of its own and toggling one must not also open a dialog. -->
      <TableFrame>
        <Table>
          <TableHeader class="sticky-head">
            <TableRow>
              <SortableHead :column="column('code')" :label="columnLabels.code!" class="ps-4" />
              <SortableHead :column="column('name')" :label="columnLabels.name!" />
              <TableHead v-if="shown('direction')" class="hidden md:table-cell">
                {{ columnLabels.direction }}
              </TableHead>
              <TableHead v-if="shown('role')" class="hidden sm:table-cell">
                {{ columnLabels.role }}
              </TableHead>
              <TableHead v-if="shown('active')">
                {{ columnLabels.active }}
              </TableHead>
              <TableHead class="w-12 pe-4">
                <span class="sr-only">{{ $t('common.actions') }}</span>
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <template v-if="pending && !languages.length">
              <TableRow v-for="n in 3" :key="n">
                <!-- One bar across the row rather than one per cell: how many cells a row shows
                   depends on the screen, and a placeholder should not have to know. -->
                <TableCell :colspan="span" class="px-4">
                  <Skeleton class="h-5 w-full" />
                </TableCell>
              </TableRow>
            </template>

            <TableEmpty v-else-if="!languages.length" :colspan="span">
              <Empty>
                <EmptyHeader>
                  <EmptyMedia variant="icon">
                    <Languages />
                  </EmptyMedia>
                  <EmptyTitle>{{ $t('languages.empty') }}</EmptyTitle>
                </EmptyHeader>
              </Empty>
            </TableEmpty>

            <!-- Languages exist, just none on this tab. -->
            <TableEmpty v-else-if="!rows.length" :colspan="span">
              <p class="py-6 text-sm text-muted-foreground">
                {{ activeFilter === 'active'
                  ? $t('languages.filters.noneActive')
                  : $t('languages.filters.noneInactive') }}
              </p>
            </TableEmpty>

            <TableRow
              v-for="{ original: language } in rows"
              v-else
              :key="language.code"
              class="cursor-pointer"
              @click="edit(language)"
            >
              <TableCell class="ps-4">
                <!-- `dir="ltr"`: a language tag is an identifier, even on a right-to-left page. -->
                <span dir="ltr" class="font-medium">{{ language.code }}</span>
              </TableCell>

              <TableCell>
                <button
                  type="button"
                  :dir="language.direction"
                  class="font-medium outline-none hover:underline focus-visible:underline"
                  @click.stop="edit(language)"
                >
                  {{ language.name }}
                </button>
              </TableCell>

              <TableCell v-if="shown('direction')" class="hidden md:table-cell">
                <Badge variant="outline" dir="ltr" class="text-muted-foreground">
                  <PilcrowLeft v-if="language.direction === 'rtl'" />
                  <PilcrowRight v-else />
                  {{ language.direction }}
                </Badge>
              </TableCell>

              <TableCell v-if="shown('role')" class="hidden sm:table-cell">
                <!-- The block's status badges: an outline, muted text, and the one thing that matters
                   carried by a coloured icon. -->
                <div class="flex flex-wrap gap-1">
                  <Badge v-if="language.isDefault" variant="outline" class="text-muted-foreground">
                    <Star class="fill-primary text-primary" />
                    {{ $t('languages.badge.default') }}
                  </Badge>
                  <Tooltip v-if="isBase(language.code)">
                    <TooltipTrigger as-child>
                      <Badge variant="outline" class="text-muted-foreground">
                        <Database class="text-primary" />
                        {{ $t('languages.badge.base') }}
                      </Badge>
                    </TooltipTrigger>
                    <TooltipContent>{{ $t('languages.badge.baseTooltip') }}</TooltipContent>
                  </Tooltip>
                </div>
              </TableCell>

              <TableCell v-if="shown('active')" @click.stop>
                <div class="flex items-center gap-2">
                  <Switch
                    :model-value="language.isActive"
                    :disabled="busy !== null"
                    :aria-label="language.isActive
                      ? $t('languages.deactivate', { name: language.name })
                      : $t('languages.activate', { name: language.name })"
                    @update:model-value="toggleActive(language, $event)"
                  />
                  <Spinner v-if="busy === language.code" class="size-3.5 text-muted-foreground" />
                </div>
              </TableCell>

              <TableCell class="pe-4 text-end" @click.stop>
                <DropdownMenu>
                  <DropdownMenuTrigger as-child>
                    <Button
                      variant="ghost"
                      size="icon-sm"
                      :aria-label="$t('languages.actionsFor', { name: language.name })"
                    >
                      <EllipsisVertical />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem @select="edit(language)">
                      <Pencil />
                      {{ $t('languages.actions.edit') }}
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      :disabled="language.isDefault || !language.isActive"
                      @select="setDefault(language.code)"
                    >
                      <Star />
                      {{ $t('languages.actions.makeDefault') }}
                    </DropdownMenuItem>
                    <DropdownMenuSeparator />
                    <DropdownMenuItem
                      variant="destructive"
                      @select="confirming = { kind: 'purge', language }"
                    >
                      <Eraser />
                      {{ $t('languages.actions.purge') }}
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      variant="destructive"
                      :disabled="language.isDefault"
                      @select="confirming = { kind: 'delete', language }"
                    >
                      <Trash2 />
                      {{ $t('languages.actions.delete') }}
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>
      </TableFrame>
    </template>

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
  </DashboardPage>
</template>
