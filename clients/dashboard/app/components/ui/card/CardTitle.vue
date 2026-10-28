<script setup lang="ts">
import type { HTMLAttributes } from 'vue'
import { cn } from 'cn'

const props = defineProps<{
  class?: HTMLAttributes['class']
}>()
</script>

<!--
  Local to this project: `leading-none font-semibold`, as the new-york-v4 card the dashboard-01 block
  is drawn with, rather than reka-vega's `leading-normal font-medium` — so a card title here reads
  at the weight it does in the block.
-->
<template>
  <div
    data-slot="card-title"
    :class="cn('text-base leading-none font-semibold group-data-[size=sm]/card:text-sm cn-font-heading', props.class)"
  >
    <slot />
  </div>
</template>
