<script setup lang="ts">
import type { RouteLocationRaw } from 'vue-router'
import { ExternalLink } from '@lucide/vue'

/**
 * ⌘K (or Ctrl+K) from anywhere: jump to a page or one of its sections, or out to the API
 * reference.
 *
 * The same groups as the sidebar, from the same `useNavigation`, with each section — a catalogue
 * tab, a settings group — as an entry of its own under its page, so "products" goes straight to
 * the Products tab rather than to the Catalog page and a click.
 *
 * shadcn-vue's `CommandDialog` filters as you type, against each item's `value` — the label, which
 * is in the dashboard's own language, so "الوسائط" finds the Media page when it is in Arabic. A
 * section's value carries its page's name too, so "settings" lists every group.
 */
const open = useCommandPalette()
const { t } = useI18n()
const { groups, resources } = useNavigation()

function go(to: RouteLocationRaw) {
  open.value = false
  navigateTo(to)
}

function leave(href: string) {
  open.value = false
  window.open(href, '_blank', 'noopener,noreferrer')
}

// Not ignored inside a text field, unlike most shortcuts: ⌘K types nothing, so there is no text to
// protect, and the palette is most wanted from the middle of a form.
useEventListener('keydown', (event: KeyboardEvent) => {
  if (event.key.toLowerCase() === 'k' && (event.metaKey || event.ctrlKey)) {
    event.preventDefault()
    open.value = !open.value
  }
})
</script>

<template>
  <CommandDialog
    v-model:open="open"
    :title="$t('nav.search')"
    :description="$t('nav.searchDescription')"
  >
    <CommandInput :placeholder="$t('nav.searchPlaceholder')" />
    <CommandList>
      <CommandEmpty>{{ $t('nav.noResults') }}</CommandEmpty>

      <!-- The top group has no label in the sidebar; here it is the first of "Go to". -->
      <CommandGroup
        v-for="group in groups"
        :key="group.label"
        :heading="group.label || t('nav.goTo')"
      >
        <template v-for="link in group.links" :key="link.id">
          <CommandItem :value="link.label" @select="go(link.to)">
            <component :is="link.icon" />
            {{ link.label }}
          </CommandItem>
          <CommandItem
            v-for="child in link.children"
            :key="`${link.id}-${child.label}`"
            :value="`${link.label} ${child.label}`"
            class="ps-8"
            @select="go(child.to)"
          >
            <span class="text-muted-foreground">{{ link.label }}</span>
            <span class="text-muted-foreground/60" aria-hidden="true">›</span>
            {{ child.label }}
          </CommandItem>
        </template>
      </CommandGroup>

      <CommandSeparator />

      <CommandGroup :heading="$t('nav.resources')">
        <CommandItem
          v-for="link in resources"
          :key="link.href"
          :value="link.label"
          @select="leave(link.href)"
        >
          <component :is="link.icon" />
          {{ link.label }}
          <ExternalLink class="ms-auto opacity-50" />
        </CommandItem>
      </CommandGroup>
    </CommandList>
  </CommandDialog>
</template>
