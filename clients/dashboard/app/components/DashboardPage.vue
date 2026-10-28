<script setup lang="ts">
/**
 * One page of the dashboard: the header across the top, an optional toolbar under it, and the
 * page's own content. The header is the `dashboard-01` block's `SiteHeader` — the sidebar toggle,
 * the page's title, and on the far side whatever the page puts in `actions`, followed by the
 * reading-language selector that every page carries.
 *
 * The header and toolbar stick to the top of the panel while the content scrolls under them, so a
 * long product list never scrolls its own "New product" button away.
 */
defineProps<{
  title: string
}>()

const { t } = useI18n()
const slots = useSlots()

/**
 * How far down the page the header (and the toolbar, when there is one) reaches — where a table's
 * sticky header row has to stop. The toolbar is a fixed `3rem` from `lg` up for exactly this: a
 * height that wraps cannot be added up in CSS.
 */
const stickyTop = computed(() => slots.toolbar
  ? 'calc(var(--header-height) + 3rem)'
  : 'var(--header-height)')
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col" :style="{ '--page-sticky-top': stickyTop }">
    <header
      class="sticky top-0 z-20 flex h-(--header-height) shrink-0 items-center gap-2 rounded-t-xl border-b bg-background/90 backdrop-blur supports-[backdrop-filter]:bg-background/70"
    >
      <div class="flex w-full min-w-0 items-center gap-1 px-4 lg:gap-2 lg:px-6">
        <SidebarTrigger class="-ms-1" :aria-label="t('nav.toggleSidebar')" />
        <Separator orientation="vertical" class="mx-2 data-[orientation=vertical]:h-4" />

        <h1 class="truncate text-base font-medium">
          {{ title }}
        </h1>

        <div class="ms-auto flex items-center gap-2">
          <slot name="actions" />
          <LanguageSelect />
        </div>
      </div>
    </header>

    <div
      v-if="$slots.toolbar"
      class="sticky top-(--header-height) z-10 flex min-h-12 shrink-0 flex-wrap items-center gap-2 border-b lg:h-12 lg:flex-nowrap bg-background/90 px-4 py-2 backdrop-blur supports-[backdrop-filter]:bg-background/70 lg:px-6"
    >
      <slot name="toolbar" />
    </div>

    <div class="@container/main flex flex-1 flex-col gap-4 p-4 md:gap-6 lg:p-6">
      <slot />
    </div>
  </div>
</template>
