<script setup lang="ts">
import type { NavigationMenuItem } from '@nuxt/ui'

const { apiReferenceUrl } = useRuntimeConfig().public
const { t } = useI18n()

// Direction is set on `<html>` by `app.vue`, which reads it from the same place this does.
useShellLocale()

const open = ref(false)
const close = () => {
  open.value = false
}

// Computed, not a constant: the labels have to be rebuilt when the language changes, and a plain
// array would hold whatever language the sidebar was first rendered in.
const links = computed(() => [[{
  label: t('nav.catalog'),
  icon: 'i-lucide-package',
  to: '/catalog',
  onSelect: close
}, {
  label: t('nav.media'),
  icon: 'i-lucide-images',
  to: '/media',
  onSelect: close
}, {
  label: t('nav.languages'),
  icon: 'i-lucide-languages',
  to: '/languages',
  onSelect: close
}, {
  label: t('nav.audit'),
  icon: 'i-lucide-history',
  to: '/audit',
  onSelect: close
}, {
  label: t('nav.settings'),
  icon: 'i-lucide-settings',
  to: '/settings',
  onSelect: close
}], [{
  label: t('nav.apiReference'),
  icon: 'i-lucide-book-open',
  to: apiReferenceUrl,
  target: '_blank'
}, {
  label: t('nav.github'),
  icon: 'i-simple-icons-github',
  to: 'https://github.com/dotnetboost',
  target: '_blank'
}]] satisfies NavigationMenuItem[][])

const groups = computed(() => [{
  id: 'links',
  label: t('nav.goTo'),
  items: links.value.flat()
}])
</script>

<template>
  <UDashboardGroup unit="rem">
    <UDashboardSidebar
      id="default"
      v-model:open="open"
      collapsible
      resizable
      class="bg-elevated/25"
      :ui="{ footer: 'lg:border-t lg:border-default' }"
    >
      <template #header="{ collapsed }">
        <AppBrand :collapsed="collapsed" />
      </template>

      <template #default="{ collapsed }">
        <UDashboardSearchButton :collapsed="collapsed" class="bg-transparent ring-default" />

        <UNavigationMenu
          :collapsed="collapsed"
          :items="links[0]"
          orientation="vertical"
          tooltip
          popover
        />

        <UNavigationMenu
          :collapsed="collapsed"
          :items="links[1]"
          orientation="vertical"
          tooltip
          class="mt-auto"
        />
      </template>

      <template #footer="{ collapsed }">
        <!--
          Who the dashboard is acting as, above the appearance menu: it is sent on every call, it
          is what the audit trail records against a change, and the Audit page cannot be read
          without it. In the shell rather than in a page header because it is not one page's
          business — a catalogue write made on another tab is attributed by the same name.
        -->
        <ActingAs :collapsed="collapsed" />
        <AppearanceMenu :collapsed="collapsed" />
      </template>
    </UDashboardSidebar>

    <UDashboardSearch :groups="groups" />

    <slot />
  </UDashboardGroup>
</template>
