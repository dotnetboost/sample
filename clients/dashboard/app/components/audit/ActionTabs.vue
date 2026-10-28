<script setup lang="ts">
import type { AuditAction } from '~/types'

/**
 * What kind of change to show, as tabs: four answers to a question asked often, each a click away
 * and the one in force visible without opening anything. Like every filter on the page, it is a
 * parameter on the API query, not a filter over the rows on screen.
 */
const action = defineModel<AuditAction | ''>({ required: true })

const { actions, icons, labels } = useAuditActions()

/**
 * "All" is a sentinel rather than an empty value: reka-ui's tabs need a value for every trigger,
 * and the query keeps "no action filter" as the empty string the API is never sent.
 */
const ALL = 'all'
</script>

<template>
  <Tabs
    :model-value="action || ALL"
    @update:model-value="action = $event === ALL ? '' : $event as AuditAction"
  >
    <TabsList :aria-label="$t('audit.filters.action')">
      <TabsTrigger :value="ALL">
        {{ $t('audit.filters.allActions') }}
      </TabsTrigger>
      <!-- Gray icons, as in the filter inputs: the colour of an action is the table's to show, in
           its badges, not the control's. -->
      <TabsTrigger v-for="each in actions" :key="each" :value="each">
        <component :is="icons[each]" class="text-muted-foreground" />
        {{ labels[each] }}
      </TabsTrigger>
    </TabsList>
  </Tabs>
</template>
