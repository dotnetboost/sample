<script setup lang="ts">
import { ArrowUpDown, Filter, FilterX, Folder, Search, Tag, X } from '@lucide/vue'
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
 * "No filter" is a sentinel rather than an empty string: a reka-ui select item with an empty value
 * is refused outright, because the component reserves that for clearing the selection.
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
  <!--
    One row from `md` up, the full width of the page, every control sharing it equally — as the
    Audit filters do, so the two pages read alike and nothing is left on a line of its own when a
    button appears beside the row. Below `md` each control takes the full width.
  -->
  <div class="flex flex-col gap-2 md:flex-row md:items-center">
    <!-- `dir="auto"` on the value boxes: see SettingsGroupForm for why. -->
    <InputGroup class="w-full md:min-w-0 md:flex-1">
      <InputGroupAddon>
        <Spinner v-if="pending" />
        <Search v-else />
      </InputGroupAddon>
      <InputGroupInput v-model="search" dir="auto" :placeholder="$t('media.filters.search')" />
      <InputGroupAddon v-if="search" align="inline-end">
        <InputGroupButton
          size="icon-xs"
          :aria-label="$t('media.filters.clearSearch')"
          @click="search = ''"
        >
          <X />
        </InputGroupButton>
      </InputGroupAddon>
    </InputGroup>

    <!-- Free text with suggestions rather than selects: the options are the values seen on the
         pages loaded so far, so a category or tag that has not appeared yet can still be typed. -->
    <SuggestInput
      :model-value="query.category"
      :options="categoryOptions"
      :icon="Folder"
      :placeholder="$t('media.filters.anyCategory')"
      class="w-full md:min-w-0 md:flex-1"
      @update:model-value="emit('update', { category: $event })"
    />

    <SuggestInput
      :model-value="query.tag"
      :options="tagOptions"
      :icon="Tag"
      :placeholder="$t('media.filters.anyTag')"
      class="w-full md:min-w-0 md:flex-1"
      @update:model-value="emit('update', { tag: $event })"
    />

    <Select v-if="!compact" :model-value="kind" @update:model-value="onKind(String($event))">
      <SelectTrigger class="w-full *:data-[slot=select-value]:flex-1 md:min-w-0 md:flex-1">
        <Filter class="text-muted-foreground" />
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        <SelectItem v-for="item in kinds" :key="item.value" :value="item.value">
          {{ item.label }}
        </SelectItem>
      </SelectContent>
    </Select>

    <Select :model-value="sort" @update:model-value="onSort($event as SortValue)">
      <SelectTrigger class="w-full *:data-[slot=select-value]:flex-1 md:min-w-0 md:flex-1">
        <ArrowUpDown class="text-muted-foreground" />
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        <SelectItem v-for="item in sorts" :key="item.value" :value="item.value">
          {{ item.label }}
        </SelectItem>
      </SelectContent>
    </Select>

    <Button
      v-if="isFiltered"
      variant="ghost"
      class="shrink-0 self-start md:self-auto"
      @click="emit('reset')"
    >
      <FilterX />
      {{ $t('common.clear') }}
    </Button>
  </div>
</template>
