<script setup lang="ts">
/**
 * The shell every page sits in, after shadcn-vue's `dashboard-01` block: an inset sidebar, and the
 * page on a raised panel beside it. Each page draws its own header inside the panel — see
 * `DashboardPage.vue` — so the title and the actions are the page's business, not the layout's.
 *
 * `--header-height` is the block's, read by `DashboardPage`; the sidebar width is shadcn-vue's
 * default of 16rem, narrower than the block's 18rem because the labels here are one or two words.
 */
</script>

<template>
  <SidebarProvider :style="{ '--header-height': 'calc(var(--spacing) * 14)' }">
    <AppSidebar />

    <SidebarInset class="min-w-0">
      <slot />
    </SidebarInset>

    <CommandPalette />
  </SidebarProvider>
</template>
