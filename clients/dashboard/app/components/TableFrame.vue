<script setup lang="ts">
/**
 * The frame every table in the dashboard sits in, after the `dashboard-01` block's data table: a
 * rounded border, and a shaded header row that stays in view while the page scrolls.
 *
 * Put the `<Table>` straight inside it, and give its `<TableHeader>` the `sticky-head` class —
 * see `assets/css/main.css`, which explains why that class has to be the frame's business.
 *
 * **Why the corners are `overflow-clip` and not `overflow-hidden`.** Both round the corners; only
 * `hidden` makes the frame a scroll container, and a sticky header sticks to its nearest scroll
 * container. Under `hidden` it would stick to the frame, which never scrolls, and so never stick.
 *
 * **Why it scrolls sideways only below `lg`.** The same rule again, for the box shadcn-vue's
 * `<Table>` wraps itself in: `overflow-x: auto` is a scroll container too. Below `lg` the tables
 * hide their minor columns and may still need to scroll sideways, so they keep it and the header
 * simply does not stick there. From `lg` up the columns fit, the box stops scrolling, and the
 * header sticks under the page's own header.
 */
</script>

<template>
  <div
    data-slot="table-frame"
    class="overflow-clip rounded-lg border bg-card lg:*:data-[slot=table-container]:overflow-visible"
  >
    <slot />
  </div>
</template>
