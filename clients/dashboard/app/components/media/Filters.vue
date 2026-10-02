<script setup lang="ts">
import { watchDebounced } from '@vueuse/core'
import type { MediaQuery, MediaSortBy } from '~/types'

/**
 * The filter bar: a search box, a category, a tag, a kind, and the sort.
 *
 * Everything here writes to the query the API is called with — nothing filters rows already on
 * screen, which would only ever search the page you are looking at.
 *
 * Changes go out as a patch rather than being written into `query` directly: the query belongs to
 * the browser that owns the listing, and a component that reaches into its parent's state is one
 * that cannot be reused anywhere the state is shaped differently.
 */
const props = defineProps<{
  query: MediaQuery
  categoryOptions: string[]
  tagOptions: string[]
  isFiltered: boolean
  pending?: boolean
  compact?: boolean
}>()

const emit = defineEmits<{
  update: [patch: Partial<MediaQuery>]
  reset: []
}>()

const { t } = useI18n()

/**
 * The search box is bound to its own ref and debounced into the query: binding it straight
 * through would fire a request per keystroke.
 */
const search = ref(props.query.search)

watchDebounced(search, value => emit('update', { search: value.trim() }), { debounce: 300 })

// reset() clears the query; the box has to follow it.
watch(() => props.query.search, (value) => {
  if (value !== search.value.trim()) {
    search.value = value
  }
})

/**
 * "No filter" is a sentinel rather than an empty string: a `USelect` item with an empty value is
 * refused outright, because the component reserves that for clearing the selection.
 */
const ANY = 'any'

// Computed, like every other list of labels in the dashboard: the values are media-type prefixes
// the API understands and never change, but what each one is called does.
const kinds = computed(() => [
  { label: t('media.filters.kinds.any'), value: ANY },
  { label: t('media.filters.kinds.images'), value: 'image/' },
  { label: t('media.filters.kinds.video'), value: 'video/' },
  { label: t('media.filters.kinds.audio'), value: 'audio/' },
  { label: t('media.filters.kinds.pdf'), value: 'application/pdf' },
  { label: t('media.filters.kinds.text'), value: 'text/' }
])

const kind = computed(() => props.query.contentType || ANY)

function onKind(value: string) {
  emit('update', { contentType: value === ANY ? '' : value })
}

type SortValue = `${MediaSortBy}:${'desc' | 'asc'}`

const sorts = computed<{ label: string, value: SortValue }[]>(() => [
  { label: t('media.filters.sorts.newest'), value: 'createdAt:desc' },
  { label: t('media.filters.sorts.oldest'), value: 'createdAt:asc' },
  { label: t('media.filters.sorts.nameAsc'), value: 'fileName:asc' },
  { label: t('media.filters.sorts.nameDesc'), value: 'fileName:desc' },
  { label: t('media.filters.sorts.largest'), value: 'size:desc' },
  { label: t('media.filters.sorts.smallest'), value: 'size:asc' }
])

const sort = computed<SortValue>(() =>
  `${props.query.sortBy}:${props.query.descending ? 'desc' : 'asc'}` as SortValue)

function onSort(value: SortValue) {
  const [by, direction] = value.split(':') as [MediaSortBy, string]
  emit('update', { sortBy: by, descending: direction === 'desc' })
}
</script>

<template>
  <div class="flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-center">
    <!-- `dir="auto"` on the value boxes: see SettingsGroupForm for why. -->
    <UInput
      v-model="search"
      dir="auto"
      icon="i-lucide-search"
      :placeholder="$t('media.filters.search')"
      :loading="pending"
      class="w-full sm:min-w-48 sm:flex-1"
      :ui="{ trailing: 'pe-1' }"
    >
      <template v-if="search" #trailing>
        <UButton
          icon="i-lucide-x"
          color="neutral"
          variant="link"
          size="sm"
          :aria-label="$t('media.filters.clearSearch')"
          @click="search = ''"
        />
      </template>
    </UInput>

    <!-- Comboboxes rather than selects: the options are the values seen on the pages loaded so
         far, so a category or tag that has not appeared yet can still be typed. -->
    <UInputMenu
      :model-value="query.category"
      :items="categoryOptions"
      create-item
      dir="auto"
      :placeholder="$t('media.filters.anyCategory')"
      icon="i-lucide-folder"
      class="w-full sm:w-44"
      @update:model-value="emit('update', { category: $event ?? '' })"
      @create="emit('update', { category: $event })"
    />

    <UInputMenu
      :model-value="query.tag"
      :items="tagOptions"
      create-item
      dir="auto"
      :placeholder="$t('media.filters.anyTag')"
      icon="i-lucide-tag"
      class="w-full sm:w-40"
      @update:model-value="emit('update', { tag: $event ?? '' })"
      @create="emit('update', { tag: $event })"
    />

    <USelect
      v-if="!compact"
      :model-value="kind"
      :items="kinds"
      icon="i-lucide-filter"
      class="w-full sm:w-36"
      @update:model-value="onKind"
    />

    <USelect
      :model-value="sort"
      :items="sorts"
      icon="i-lucide-arrow-up-down"
      class="w-full sm:w-40"
      @update:model-value="onSort"
    />

    <UButton
      v-if="isFiltered"
      icon="i-lucide-filter-x"
      color="neutral"
      variant="ghost"
      :label="$t('common.clear')"
      class="self-start"
      @click="emit('reset')"
    />
  </div>
</template>
