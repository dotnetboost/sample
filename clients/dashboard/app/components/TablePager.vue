<script setup lang="ts">
import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from '@lucide/vue'

/**
 * A table's footer bar, inside its `TableFrame`, as the `dashboard-01` block draws one: whatever
 * the table wants to say about its rows on the start side (the default slot), and the page
 * controls on the end side — the `size` slot for a rows-per-page control or a note about who
 * decides it, "Page 2 of 7", and first/previous/next/last.
 *
 * Pages are the API's in every table here, so this only reports a move; the page asks for it.
 * First and last are wide-screen only, and every chevron is mirrored in Arabic, where the first
 * page is to the right.
 */
defineProps<{
  pageCount: number
  /** While a page is loading, so a second click cannot queue a second move. */
  disabled?: boolean
}>()

const page = defineModel<number>('page', { required: true })
</script>

<template>
  <div class="flex flex-wrap items-center justify-between gap-x-6 gap-y-2 border-t px-4 py-3 text-sm">
    <div class="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1 text-muted-foreground">
      <slot />
    </div>

    <div class="ms-auto flex items-center gap-4 lg:gap-6">
      <slot name="size" />

      <template v-if="pageCount > 1">
        <span class="font-medium tabular-nums">
          {{ $t('common.pageOf', { page, count: pageCount }) }}
        </span>

        <div class="flex items-center gap-1">
          <Button
            variant="outline"
            size="icon-sm"
            class="hidden lg:inline-flex"
            :disabled="disabled || page <= 1"
            :aria-label="$t('common.firstPage')"
            @click="page = 1"
          >
            <ChevronsLeft class="rtl:rotate-180" />
          </Button>
          <Button
            variant="outline"
            size="icon-sm"
            :disabled="disabled || page <= 1"
            :aria-label="$t('common.previous')"
            @click="page = page - 1"
          >
            <ChevronLeft class="rtl:rotate-180" />
          </Button>
          <Button
            variant="outline"
            size="icon-sm"
            :disabled="disabled || page >= pageCount"
            :aria-label="$t('common.next')"
            @click="page = page + 1"
          >
            <ChevronRight class="rtl:rotate-180" />
          </Button>
          <Button
            variant="outline"
            size="icon-sm"
            class="hidden lg:inline-flex"
            :disabled="disabled || page >= pageCount"
            :aria-label="$t('common.lastPage')"
            @click="page = pageCount"
          >
            <ChevronsRight class="rtl:rotate-180" />
          </Button>
        </div>
      </template>
    </div>
  </div>
</template>
