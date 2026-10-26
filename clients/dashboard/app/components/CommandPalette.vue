<script setup lang="ts">
import { ExternalLink } from '@lucide/vue'
import type { NavLink } from '~/composables/useNavigation'

/**
 * ⌘K (or Ctrl+K) from anywhere: jump to a page, or out to the API reference.
 *
 * shadcn-vue's `CommandDialog` filters as you type, against each item's `value` — the label, which
 * is in the dashboard's own language, so "الوسائط" finds the Media page when it is in Arabic.
 */
const open = useCommandPalette()
const { main, resources } = useNavigation()

function go(link: NavLink) {
  open.value = false

  if (link.external) {
    window.open(link.to, '_blank', 'noopener,noreferrer')
  } else {
    navigateTo(link.to)
  }
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

      <CommandGroup :heading="$t('nav.goTo')">
        <CommandItem
          v-for="link in main"
          :key="link.to"
          :value="link.label"
          @select="go(link)"
        >
          <component :is="link.icon" />
          {{ link.label }}
        </CommandItem>
      </CommandGroup>

      <CommandSeparator />

      <CommandGroup :heading="$t('nav.resources')">
        <CommandItem
          v-for="link in resources"
          :key="link.to"
          :value="link.label"
          @select="go(link)"
        >
          <component :is="link.icon" />
          {{ link.label }}
          <ExternalLink class="ms-auto opacity-50" />
        </CommandItem>
      </CommandGroup>
    </CommandList>
  </CommandDialog>
</template>
