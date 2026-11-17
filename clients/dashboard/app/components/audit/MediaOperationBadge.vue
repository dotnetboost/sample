<script setup lang="ts">
import type { AuditEntry } from '~/types'

const props = defineProps<{
  entry: AuditEntry
  /** On a phone, the icon alone — for the table, where the row has to fit. */
  compact?: boolean
}>()

const { icon, label } = useMediaOperations()

/**
 * Every media change is an `Updated` entry on its owner, because those are the only actions the
 * auditing library has; the operation itself is in the entry's metadata. This draws it beside the
 * action, so an attach and a reorder do not look alike.
 */
const info = computed(() => mediaOperationOf(props.entry))
</script>

<template>
  <!-- In the table the short name, so the action column stays as narrow as the action's own badge;
       the full one is on hover and in the entry's details. -->
  <Badge
    v-if="info"
    variant="secondary"
    class="text-muted-foreground"
    :title="label(info)"
  >
    <component :is="icon(info)" class="text-primary" />
    <span :class="{ 'max-sm:sr-only': compact }">{{ label(info, compact) }}</span>
  </Badge>
</template>
