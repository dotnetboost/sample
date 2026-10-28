<script setup lang="ts">
import { CircleSlash, Clock, Infinity as Forever } from '@lucide/vue'
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
  { rule: 'Record', badge: 'bg-success/15 text-success', explanation: t('audit.rules.record') },
  { rule: 'Redact', badge: 'bg-warning/15 text-warning', explanation: t('audit.rules.redact') },
  { rule: 'Ignore', badge: 'bg-secondary text-secondary-foreground', explanation: t('audit.rules.ignore') }
] as const)
  .map(group => ({
    ...group,
    properties: props.schema.properties.filter(property => property.rule === group.rule)
  }))
  .filter(group => group.properties.length))
</script>

<template>
  <Card size="sm">
    <CardHeader>
      <CardTitle>{{ $t('audit.rules.title', { entity: schema.name }) }}</CardTitle>
      <CardDescription>{{ $t('audit.rules.description') }}</CardDescription>
    </CardHeader>

    <CardContent class="flex flex-col gap-3">
      <div class="flex flex-wrap items-center gap-2">
        <Badge variant="outline" dir="ltr">
          {{ schema.clrType }}
        </Badge>

        <Badge v-if="!schema.audited" class="bg-warning/15 text-warning">
          <CircleSlash />
          {{ $t('audit.rules.notAudited') }}
        </Badge>

        <!-- `null` retention means the library has nothing configured either way, so ITS prune
             would leave these entries alone — never that they expire immediately. What prunes in
             this application is the API's retention service, reading the `auditing` settings
             group, so that is the second answer below and the one that applies here. -->
        <Badge v-if="schema.retentionDays !== null" class="bg-primary/10 text-primary">
          <Clock />
          {{ schema.hasOwnRetention
            ? $t('audit.rules.retentionOwn', schema.retentionDays)
            : $t('audit.rules.retentionDefault', schema.retentionDays) }}
        </Badge>
        <Badge v-else-if="prunedAfterDays !== null" class="bg-primary/10 text-primary">
          <Clock />
          {{ $t('audit.rules.retentionPruned', prunedAfterDays) }}
        </Badge>
        <Badge v-else variant="secondary">
          <Forever />
          {{ $t('audit.rules.retentionNone') }}
        </Badge>
      </div>

      <div
        v-for="group in groups"
        :key="group.rule"
        class="flex flex-wrap items-baseline gap-2"
      >
        <Badge :class="group.badge" dir="ltr">
          {{ group.rule }}
        </Badge>

        <span class="text-xs text-muted-foreground">{{ group.explanation }}</span>

        <div class="flex flex-wrap gap-1">
          <!-- Property names are the C# ones — the identity this endpoint and a recorded change's
               `propertyName` share — so they are quoted, not translated. -->
          <span
            v-for="property in group.properties"
            :key="property.name"
            dir="ltr"
            class="rounded bg-muted px-1.5 py-0.5 text-xs"
          >{{ property.name }}</span>
        </div>
      </div>
    </CardContent>
  </Card>
</template>
