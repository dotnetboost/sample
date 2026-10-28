<script setup lang="ts">
import type { HTMLAttributes } from 'vue'
import { cn } from 'cn'

const props = defineProps<{
  class?: HTMLAttributes['class']
}>()
</script>

<template>
  <ul
    data-slot="sidebar-menu-sub"
    data-sidebar="menu-sub"
    :class="cn(
      'border-sidebar-border mx-3.5 translate-x-px rtl:-translate-x-px gap-1 border-s px-2.5 py-0.5 group-data-[collapsible=icon]:hidden flex min-w-0 flex-col',
      props.class
    )"
  >
    <slot />
  </ul>
</template>
