<script setup lang="ts">
import type { AuditAction } from '~/types'

defineProps<{
  action: AuditAction
}>()

const { icons, styles, labels } = useAuditActions()
</script>

<template>
  <!-- On a phone the icon alone, so the row fits without scrolling sideways; the word stays for
       assistive technology. -->
  <Badge variant="outline" class="text-muted-foreground">
    <component :is="icons[action]" :class="styles[action]" />
    <span class="max-sm:sr-only">{{ labels[action] }}</span>
  </Badge>
</template>
