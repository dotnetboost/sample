<script setup lang="ts">
import type { AuditEntitySchema } from '~/types'

/**
 * What the model says is recorded for one entity, from `GET api/audit/entities/{entityName}`.
 *
 * This is the model's own description and never a summary of the entries, which is what makes it
 * worth a card of its own: it answers before the trail has a single row in it, and it is where this
 * application's one deliberate omission becomes visible rather than inferred.
 *
 * `Category.Name`, `Category.Description`, `Product.Name` and `Product.Description` carry
 * `[AuditIgnore]`, so they are reported as `Ignore` — a translated value depends on the language
 * and an entry has no language, so an entry saying `Name` changed could not say in which. Someone
 * who edits a product's Arabic text, sees no new entry and has nothing to read would reasonably
 * conclude the recording is broken. This is the answer, next to the trail.
 */
const props = defineProps<{
  schema: AuditEntitySchema
}>()

const { t } = useI18n()

/**
 * The retention the API's prune applies, when the `auditing` settings group has it on.
 *
 * Read only when the model has no retention of its own to report — which, in this application, is
 * always: see `useAuditingSettings` for why the library's option stays unset. A failed read falls
 * back to the model's answer rather than guessing.
 */
const { data: auditing } = await useAuditingSettings()

const prunedAfterDays = computed(() =>
  auditing.value?.pruneEnabled ? auditing.value.retentionDays : null)

/**
 * The properties by rule, in the order the three are worth reading: recorded, withheld, absent.
 *
 * A rule with no properties under it is dropped rather than drawn empty — `Redact` is empty in this
 * application, because nothing in the catalogue is marked sensitive.
 */
const groups = computed(() => ([
  { rule: 'Record', color: 'success', explanation: t('audit.rules.record') },
  { rule: 'Redact', color: 'warning', explanation: t('audit.rules.redact') },
  { rule: 'Ignore', color: 'neutral', explanation: t('audit.rules.ignore') }
] as const)
  .map(group => ({
    ...group,
    properties: props.schema.properties.filter(property => property.rule === group.rule)
  }))
  .filter(group => group.properties.length))
</script>

<template>
  <UPageCard
    :title="$t('audit.rules.title', { entity: schema.name })"
    :description="$t('audit.rules.description')"
    variant="subtle"
    :ui="{ container: 'gap-3' }"
  >
    <div class="flex flex-col gap-3">
      <div class="flex flex-wrap items-center gap-2">
        <UBadge
          color="neutral"
          variant="subtle"
          dir="ltr"
          class="font-mono"
          :label="schema.clrType"
        />

        <UBadge
          v-if="!schema.audited"
          color="warning"
          variant="subtle"
          icon="i-lucide-circle-slash"
          :label="$t('audit.rules.notAudited')"
        />

        <!-- `null` retention means the library has nothing configured either way, so ITS prune
             would leave these entries alone — never that they expire immediately. What prunes in
             this application is the API's retention service, reading the `auditing` settings
             group, so that is the second answer below and the one that applies here. -->
        <UBadge
          v-if="schema.retentionDays !== null"
          color="info"
          variant="subtle"
          icon="i-lucide-clock"
          :label="schema.hasOwnRetention
            ? $t('audit.rules.retentionOwn', schema.retentionDays)
            : $t('audit.rules.retentionDefault', schema.retentionDays)"
        />
        <UBadge
          v-else-if="prunedAfterDays !== null"
          color="info"
          variant="subtle"
          icon="i-lucide-clock"
          :label="$t('audit.rules.retentionPruned', prunedAfterDays)"
        />
        <UBadge
          v-else
          color="neutral"
          variant="subtle"
          icon="i-lucide-infinity"
          :label="$t('audit.rules.retentionNone')"
        />
      </div>

      <div
        v-for="group in groups"
        :key="group.rule"
        class="flex flex-wrap items-baseline gap-2"
      >
        <UBadge
          :color="group.color"
          variant="subtle"
          class="font-mono"
          dir="ltr"
          :label="group.rule"
        />

        <span class="text-xs text-muted">{{ group.explanation }}</span>

        <div class="flex flex-wrap gap-1">
          <!-- Property names are the C# ones — the identity this endpoint and a recorded change's
               `propertyName` share — so they are quoted, not translated. -->
          <span
            v-for="property in group.properties"
            :key="property.name"
            dir="ltr"
            class="rounded bg-elevated px-1.5 py-0.5 font-mono text-xs text-highlighted"
          >{{ property.name }}</span>
        </div>
      </div>
    </div>
  </UPageCard>
</template>
