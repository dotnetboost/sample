<script setup lang="ts">
import { ArrowDown, ArrowUp, ChevronsUpDown } from '@lucide/vue'

/**
 * A column header that sorts its column — the `data-table` pattern from shadcn-vue's docs: the
 * label as a ghost button, and an arrow that says which way the column is sorted, or a faint
 * up-and-down one when it is not.
 *
 * Takes the TanStack column rather than a direction and a callback, so the cycle and the state
 * stay the table's business. Typed by shape, because what it needs is three methods and the
 * column's generic type would tie this component to one table's features and row type.
 *
 * `aria-sort` goes on the `<th>` itself — that is where assistive technology looks for it — and
 * the button's name says what it does, since its text alone is just the column's name.
 */
const props = defineProps<{
  column: {
    getCanSort: () => boolean
    getIsSorted: () => false | 'asc' | 'desc'
    toggleSorting: (desc?: boolean) => void
    getNextSortingOrder: () => false | 'asc' | 'desc'
  }
  label: string
  /** For a numeric column, whose header sits at the end of its cell like the numbers under it. */
  align?: 'start' | 'end'
}>()

const sorted = computed(() => props.column.getIsSorted())

const ariaSort = computed(() =>
  sorted.value === 'asc' ? 'ascending' : sorted.value === 'desc' ? 'descending' : 'none')

function toggle() {
  const next = props.column.getNextSortingOrder()
  if (next) props.column.toggleSorting(next === 'desc')
}
</script>

<template>
  <TableHead :aria-sort="column.getCanSort() ? ariaSort : undefined" :class="align === 'end' && 'text-end'">
    <Button
      v-if="column.getCanSort()"
      variant="ghost"
      size="sm"
      class="-mx-2.5 h-8 font-medium data-[sorted=true]:text-foreground"
      :data-sorted="!!sorted"
      :aria-label="$t('common.sortBy', { column: label })"
      @click="toggle"
    >
      {{ label }}
      <ArrowUp v-if="sorted === 'asc'" />
      <ArrowDown v-else-if="sorted === 'desc'" />
      <ChevronsUpDown v-else class="opacity-40" />
    </Button>
    <template v-else>
      {{ label }}
    </template>
  </TableHead>
</template>
