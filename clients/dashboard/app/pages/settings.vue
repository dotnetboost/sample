<script setup lang="ts">
import type { NavigationMenuItem } from '@nuxt/ui'

const { apiReferenceUrl } = useRuntimeConfig().public

// One tab per settings group. Adding a group to `settingsGroups` (and a page under
// `pages/settings/`) is all it takes to get another — nothing here enumerates them.
const links = [
  settingsGroups.map(group => ({
    label: group.label,
    icon: group.icon,
    to: group.to,
    exact: group.to === '/settings'
  })),
  [{
    label: 'API reference',
    icon: 'i-lucide-book-open',
    to: apiReferenceUrl,
    target: '_blank'
  }]
] satisfies NavigationMenuItem[][]

useHead({ title: 'Settings — DotNetBoost Sample' })
</script>

<template>
  <UDashboardPanel id="settings" :ui="{ body: 'lg:py-12' }">
    <template #header>
      <UDashboardNavbar title="Settings">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>

        <template #right>
          <LanguageSelect />
        </template>
      </UDashboardNavbar>

      <UDashboardToolbar>
        <!-- NOTE: `-mx-1` aligns this with the DashboardSidebarCollapse button above. -->
        <UNavigationMenu :items="links" highlight class="-mx-1 flex-1" />
      </UDashboardToolbar>
    </template>

    <template #body>
      <div class="mx-auto flex w-full flex-col gap-4 sm:gap-6 lg:max-w-2xl lg:gap-12">
        <NuxtPage />
      </div>
    </template>
  </UDashboardPanel>
</template>
