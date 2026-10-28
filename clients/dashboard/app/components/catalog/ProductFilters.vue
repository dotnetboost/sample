<script setup lang="ts">
import { FilterX, FolderTree, Search, X } from '@lucide/vue'
import type { Category } from '~/types'

/**
 * The products filter bar: a search box and a category, with the table's own actions at the end
 * through the default slot. What the filters mean is `useProductList`'s; this only draws them.
 */
const props = defineProps<{
  categories: Category[]
  /** Whether a filter is in force, which is when Clear is offered. */
  filtered: boolean
  /** Whether a read is in flight, which the search box shows while there is text in it. */
  pending: boolean
}>()

const search = defineModel<string>('search', { required: true })
const categoryId = defineModel<number | undefined>('categoryId', { required: true })

const emit = defineEmits<{
  clear: []
}>()

const categoryName = computed(() =>
  props.categories.find(c => c.id === categoryId.value)?.name ?? '—')
</script>

<template>
  <div class="flex flex-col gap-2 sm:flex-row sm:items-center">
    <!-- `dir="auto"`: what is typed may be Arabic or a Latin SKU, either way round. -->
    <InputGroup class="w-full sm:max-w-sm">
      <InputGroupAddon>
        <Spinner v-if="pending && search" />
        <Search v-else />
      </InputGroupAddon>
      <InputGroupInput
        v-model="search"
        dir="auto"
        :placeholder="$t('catalog.filters.search')"
        :aria-label="$t('catalog.filters.search')"
      />
      <InputGroupAddon v-if="search" align="inline-end">
        <InputGroupButton size="icon-xs" :aria-label="$t('catalog.filters.clearSearch')" @click="search = ''">
          <X />
        </InputGroupButton>
      </InputGroupAddon>
    </InputGroup>

    <!-- "All categories" is a sentinel rather than an empty value, which reka-ui's select
         refuses because it reserves that for clearing the selection. -->
    <Select
      :model-value="categoryId ?? 'all'"
      @update:model-value="categoryId = $event === 'all' ? undefined : Number($event)"
    >
      <SelectTrigger class="w-full sm:w-52" :aria-label="$t('catalog.filters.category')">
        <FolderTree class="text-muted-foreground" />
        <!-- The name by itself: left to reka-ui, the trigger repeats the whole option, the
             product count beside the name included. -->
        <SelectValue>
          <span dir="auto">{{ categoryId === undefined ? $t('catalog.filters.allCategories') : categoryName }}</span>
        </SelectValue>
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="all">
          {{ $t('catalog.filters.allCategories') }}
        </SelectItem>
        <SelectSeparator />
        <SelectItem
          v-for="category in categories"
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
      v-if="filtered"
      variant="ghost"
      class="self-start sm:self-auto"
      @click="emit('clear')"
    >
      <FilterX />
      {{ $t('common.clear') }}
    </Button>

    <div class="flex items-center justify-end gap-2 sm:ms-auto">
      <slot />
    </div>
  </div>
</template>
