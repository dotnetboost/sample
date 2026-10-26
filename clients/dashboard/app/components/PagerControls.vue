<script setup lang="ts">
import { ChevronLeft, ChevronRight } from '@lucide/vue'

/**
 * A numbered pager — what `UPagination` drew under Nuxt UI — assembled from shadcn-vue's
 * pagination parts. One place for it, because the catalogue, the media library and the audit
 * trail all page the same way: `page` is 1-based, `total` is the API's count, and the page size is
 * whatever the API said it used.
 *
 * The previous and next buttons are icons with a label for assistive technology, because the
 * parts' own text is a hard-coded English "Previous"; the chevrons are mirrored in Arabic.
 */
defineProps<{
  total: number
  itemsPerPage: number
  /** While a page is loading, so a second click cannot queue a second move. */
  disabled?: boolean
}>()

const page = defineModel<number>('page', { required: true })
</script>

<template>
  <Pagination
    v-slot="{ page: current }"
    v-model:page="page"
    :total="total"
    :items-per-page="itemsPerPage"
    :sibling-count="1"
    :disabled="disabled"
    show-edges
    class="mx-0 w-auto"
  >
    <PaginationContent v-slot="{ items }">
      <PaginationPrevious size="icon-sm" class="ps-0!" :aria-label="$t('common.previous')">
        <ChevronLeft class="rtl:rotate-180" />
      </PaginationPrevious>

      <template v-for="(item, index) in items" :key="index">
        <PaginationItem
          v-if="item.type === 'page'"
          :value="item.value"
          :is-active="item.value === current"
          size="icon-sm"
        >
          {{ item.value }}
        </PaginationItem>
        <PaginationEllipsis v-else :index="index" />
      </template>

      <PaginationNext size="icon-sm" class="pe-0!" :aria-label="$t('common.next')">
        <ChevronRight class="rtl:rotate-180" />
      </PaginationNext>
    </PaginationContent>
  </Pagination>
</template>
