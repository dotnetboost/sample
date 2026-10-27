<script setup lang="ts">
import { parseDate } from '@internationalized/date'
import type { CalendarDate } from '@internationalized/date'
import { Box, CalendarRange, CircleSlash, FilterX, KeyRound, UserRound, X } from '@lucide/vue'
import { watchDebounced } from '@vueuse/core'
import type { DateRange } from 'reka-ui'
import type { AuditEntityDescriptor, AuditQuery } from '~/types'

/**
 * The filter bar: the entity, its key, the actor and a date range. The action is the tabs over the
 * table, the sort is the table's column headers and the page size is in its footer — all in
 * `audit.vue`, beside the table they act on.
 *
 * Every one of them is a parameter on `GET api/audit`. Nothing here filters the rows already on
 * screen — a trail only grows, so the page you are looking at is never the set you meant to
 * search, and "actor: alice" over one page of twenty-five would answer for the wrong set with no
 * sign that it had.
 *
 * Changes leave as a patch, like the media browser's: the query belongs to the composable that
 * owns the listing, and a component reaching into its parent's state is one that cannot be reused
 * anywhere the state is shaped differently.
 */
const props = defineProps<{
  query: AuditQuery
  /** From `GET api/audit/entities` — the entities this installation declares. */
  entities: AuditEntityDescriptor[]
  isFiltered: boolean
  pending?: boolean
}>()

const emit = defineEmits<{
  update: [patch: Partial<AuditQuery>]
  reset: []
}>()

const { t } = useI18n()

/**
 * "Any" is a sentinel rather than an empty string: a reka-ui select item with an empty value is
 * refused outright, because the component reserves that for clearing the selection.
 */
const ANY = 'any'

/**
 * The entities the API declares, and nothing else.
 *
 * Not a list written down here: which entities are declared, and whether each is audited, is the
 * engine's own answer — it accounts for the attributes, for opt-in mode and for the kill switch.
 * This installation declares `Category` and `Product`. The trail is wider than that — the API's
 * capture also records `Setting`, `MediaFile` and the other libraries' rows, which "Any" shows —
 * and `Translation` is the one entity it never records.
 */
const entityItems = computed(() => [
  { label: t('audit.filters.anyEntity'), value: ANY, audited: true },
  // `audited` travels with the option so the list can mark a declared entity whose changes are not
  // currently being recorded — an empty trail for one of those is the engine's answer, not a
  // quiet nothing.
  ...props.entities.map(entity => ({
    label: entity.name,
    value: entity.name,
    audited: entity.audited
  }))
])

const entity = computed(() => props.query.entityName || ANY)

/**
 * `from` and `to` as the range the calendar draws. The query keeps them as `yyyy-mm-dd`, which is
 * what `CalendarDate` reads and writes, and `useAuditTrail` turns into the instants the API is
 * asked for.
 */
const range = computed(() => ({
  start: props.query.from ? parseDate(props.query.from) : undefined,
  end: props.query.to ? parseDate(props.query.to) : undefined
}) as DateRange)

/**
 * A range is sent once it has both ends. The calendar reports the first click on its own, and
 * sending that would read the trail from a date to forever — one request for a range nobody
 * picked yet.
 */
function updateRange(value: DateRange | null | undefined) {
  const { start, end } = (value ?? {}) as { start?: CalendarDate, end?: CalendarDate }

  if (!start && !end) {
    emit('update', { from: '', to: '' })
  } else if (start && end) {
    emit('update', { from: start.toString(), to: end.toString() })
  }
}

/** The trigger's text, in the UI locale through `utils/format.ts`'s own formatter. */
const rangeLabel = computed(() => {
  const { from, to } = props.query

  if (!from && !to) {
    return t('audit.filters.anyDate')
  }

  return `${from ? formatDay(from) : '…'} – ${to ? formatDay(to) : '…'}`
})

/**
 * The two free-text boxes are bound to their own refs and debounced into the query: bound straight
 * through, each keystroke would be a page of the trail.
 */
const actor = ref(props.query.actor)
const entityId = ref(props.query.entityId)

watchDebounced(actor, value => emit('update', { actor: value.trim() }), { debounce: 300 })
watchDebounced(entityId, value => emit('update', { entityId: value.trim() }), { debounce: 300 })

// `reset()` and "the rest of this edit" both clear the query; the boxes have to follow.
watch(() => props.query.actor, (value) => {
  if (value !== actor.value.trim()) {
    actor.value = value
  }
})

watch(() => props.query.entityId, (value) => {
  if (value !== entityId.value.trim()) {
    entityId.value = value
  }
})
</script>

<template>
  <div class="flex flex-col gap-3">
    <div class="flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-center">
      <Select
        :model-value="entity"
        @update:model-value="emit('update', { entityName: $event === ANY ? '' : String($event) })"
      >
        <SelectTrigger class="w-full sm:w-44" :aria-label="$t('audit.filters.entity')">
          <Box class="text-muted-foreground" />
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem
            v-for="item in entityItems"
            :key="item.value"
            :value="item.value"
            :text-value="item.label"
          >
            {{ item.label }}
            <!-- `audited` travels with the option so a declared entity whose changes are not being
                 recorded is marked as such in the list itself. -->
            <CircleSlash
              v-if="!item.audited"
              class="ms-auto text-muted-foreground"
              :aria-label="$t('audit.filters.notAudited')"
            />
          </SelectItem>
        </SelectContent>
      </Select>

      <!-- `dir="ltr"` on both: a key and an actor are identifiers the API stores verbatim. -->
      <InputGroup class="w-full sm:w-36">
        <InputGroupAddon>
          <KeyRound />
        </InputGroupAddon>
        <InputGroupInput
          v-model="entityId"
          dir="ltr"
          autocomplete="off"
          :placeholder="$t('audit.filters.entityId')"
          :aria-label="$t('audit.filters.entityId')"
        />
      </InputGroup>

      <InputGroup class="w-full sm:min-w-44 sm:flex-1">
        <InputGroupAddon>
          <Spinner v-if="pending" />
          <UserRound v-else />
        </InputGroupAddon>
        <InputGroupInput
          v-model="actor"
          dir="ltr"
          autocomplete="off"
          :placeholder="$t('audit.filters.actor')"
          :aria-label="$t('audit.filters.actor')"
        />
      </InputGroup>
    </div>

    <!-- One aligned row of controls, each named for assistive technology and by its icon on
         screen, with the one thing that needs saying about dates written out under it. -->
    <div class="flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-center">
      <!-- One control for both ends: a range is one question, and two boxes let it be asked
           backwards. The value is still the `yyyy-mm-dd` pair the query has always held. -->
      <div class="flex items-center gap-1">
        <Popover>
          <PopoverTrigger as-child>
            <Button
              variant="outline"
              class="w-full justify-start font-normal sm:w-64"
              :class="{ 'text-muted-foreground': !query.from && !query.to }"
              :aria-label="$t('audit.filters.dates')"
            >
              <CalendarRange />
              <span class="truncate">{{ rangeLabel }}</span>
            </Button>
          </PopoverTrigger>
          <PopoverContent class="w-auto p-0" align="start">
            <RangeCalendar
              :model-value="range"
              :number-of-months="2"
              @update:model-value="updateRange"
            />
          </PopoverContent>
        </Popover>

        <Button
          v-if="query.from || query.to"
          variant="ghost"
          size="icon"
          :aria-label="$t('audit.filters.clearDates')"
          @click="emit('update', { from: '', to: '' })"
        >
          <X />
        </Button>
      </div>

      <Button v-if="isFiltered" variant="ghost" @click="emit('reset')">
        <FilterX />
        {{ $t('common.clear') }}
      </Button>
    </div>

    <p class="text-xs text-muted-foreground">
      {{ $t('audit.filters.datesHelp') }}
    </p>
  </div>
</template>
