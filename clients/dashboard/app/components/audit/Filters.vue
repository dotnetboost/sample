<script setup lang="ts">
import { watchDebounced } from '@vueuse/core'
import type { AuditAction, AuditEntityDescriptor, AuditQuery } from '~/types'

/**
 * The filter bar: the entity, its key, the actor, the action, a date range and the page size.
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
  /** From `GET api/audit/entities` — what this installation actually records. */
  entities: AuditEntityDescriptor[]
  /** The API's own bound on a page, from the same response. 0 until it has answered. */
  maxPageSize: number
  isFiltered: boolean
  pending?: boolean
}>()

const emit = defineEmits<{
  update: [patch: Partial<AuditQuery>]
  pageSize: [size: number]
  reset: []
}>()

const { t } = useI18n()

/**
 * "Any" is a sentinel rather than an empty string: a `USelect` item with an empty value is refused
 * outright, because the component reserves that for clearing the selection.
 */
const ANY = 'any'

/**
 * The entities the API says it records, and nothing else.
 *
 * Not a list written down here: which entities are audited is the engine's own answer — it accounts
 * for the attributes, for opt-in mode and for the kill switch — and this installation's two are
 * `Category` and `Product` while `Setting`, `MediaFile` and `Translation` are rows in the same
 * database that are deliberately not in the trail.
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

const actions = computed<{ label: string, value: AuditAction | typeof ANY }[]>(() => [
  { label: t('audit.filters.anyAction'), value: ANY },
  { label: t('audit.actions.Created'), value: 'Created' },
  { label: t('audit.actions.Updated'), value: 'Updated' },
  { label: t('audit.actions.Deleted'), value: 'Deleted' }
])

const action = computed(() => props.query.action || ANY)

/**
 * The sizes the API will actually serve, which is why `maxPageSize` is read rather than assumed.
 * A size above the cap is not refused — it is clamped — so offering one would leave the box
 * disagreeing with the `take` the response reports.
 */
const pageSizes = computed(() =>
  [10, 25, 50, 100]
    .filter(size => !props.maxPageSize || size <= props.maxPageSize)
    .map(size => ({ label: String(size), value: size })))

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
      <USelect
        :model-value="entity"
        :items="entityItems"
        value-key="value"
        icon="i-lucide-box"
        class="w-full sm:w-40"
        :aria-label="$t('audit.filters.entity')"
        @update:model-value="emit('update', { entityName: $event === ANY ? '' : $event })"
      >
        <template #item-trailing="{ item }">
          <UTooltip v-if="!item.audited" :text="$t('audit.filters.notAudited')">
            <UIcon name="i-lucide-circle-slash" class="size-4 text-dimmed" />
          </UTooltip>
        </template>
      </USelect>

      <!-- `dir="ltr"` on both: a key and an actor are identifiers the API stores verbatim. -->
      <UInput
        v-model="entityId"
        dir="ltr"
        icon="i-lucide-key-round"
        autocomplete="off"
        :placeholder="$t('audit.filters.entityId')"
        :aria-label="$t('audit.filters.entityId')"
        class="w-full font-mono sm:w-32"
      />

      <UInput
        v-model="actor"
        dir="ltr"
        icon="i-lucide-user-round"
        autocomplete="off"
        :loading="pending"
        :placeholder="$t('audit.filters.actor')"
        :aria-label="$t('audit.filters.actor')"
        class="w-full sm:min-w-44 sm:flex-1"
      />

      <USelect
        :model-value="action"
        :items="actions"
        value-key="value"
        icon="i-lucide-filter"
        class="w-full sm:w-36"
        :aria-label="$t('audit.filters.action')"
        @update:model-value="emit('update', { action: $event === ANY ? '' : $event as AuditAction })"
      />
    </div>

    <div class="flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-end">
      <UFormField :label="$t('audit.filters.from')" :help="$t('audit.filters.datesHelp')">
        <!-- A native date input: it is already localized and keyboard-accessible, and the value it
             produces (`yyyy-mm-dd`) is the one `useAuditTrail` turns into the instants the API is
             asked for. `dir="ltr"` because the parts of a date are laid out by the control. -->
        <UInput
          :model-value="query.from"
          type="date"
          dir="ltr"
          class="w-full sm:w-40"
          :max="query.to || undefined"
          @update:model-value="emit('update', { from: String($event) })"
        />
      </UFormField>

      <UFormField :label="$t('audit.filters.to')">
        <UInput
          :model-value="query.to"
          type="date"
          dir="ltr"
          class="w-full sm:w-40"
          :min="query.from || undefined"
          @update:model-value="emit('update', { to: String($event) })"
        />
      </UFormField>

      <UFormField :label="$t('audit.filters.pageSize')" class="sm:ms-auto">
        <USelect
          :model-value="query.pageSize"
          :items="pageSizes"
          value-key="value"
          icon="i-lucide-rows-3"
          class="w-full sm:w-24"
          @update:model-value="emit('pageSize', Number($event))"
        />
      </UFormField>

      <UButton
        v-if="isFiltered"
        icon="i-lucide-filter-x"
        color="neutral"
        variant="ghost"
        :label="$t('common.clear')"
        @click="emit('reset')"
      />
    </div>
  </div>
</template>
