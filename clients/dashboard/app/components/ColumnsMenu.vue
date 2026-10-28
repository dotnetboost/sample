<script setup lang="ts">
import { Columns3 } from '@lucide/vue'

/**
 * The `dashboard-01` block's "Customize columns": a button over a table's end corner, and a
 * checklist of the columns that may be hidden. Shared by every table that offers it, so the three
 * look and behave alike.
 *
 * Takes TanStack columns by shape — an id and two methods — so it is tied to no one table's
 * features or row type. The menu stays open while boxes are ticked: hiding two columns should not
 * take two trips.
 */
defineProps<{
  columns: {
    id: string
    getIsVisible: () => boolean
    toggleVisibility: (value?: boolean) => void
  }[]
  /** Each column's name, by id. */
  labels: Record<string, string>
}>()
</script>

<template>
  <!-- Default height, not `sm`: it sits at the end of filter bars whose inputs are. -->
  <DropdownMenu>
    <DropdownMenuTrigger as-child>
      <Button variant="outline">
        <Columns3 />
        <span class="max-sm:sr-only">{{ $t('common.columns') }}</span>
      </Button>
    </DropdownMenuTrigger>
    <DropdownMenuContent align="end" class="w-48">
      <DropdownMenuCheckboxItem
        v-for="column in columns"
        :key="column.id"
        :model-value="column.getIsVisible()"
        @update:model-value="column.toggleVisibility(!!$event)"
        @select.prevent
      >
        {{ labels[column.id] }}
      </DropdownMenuCheckboxItem>
    </DropdownMenuContent>
  </DropdownMenu>
</template>
