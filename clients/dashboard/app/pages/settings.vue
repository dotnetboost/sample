<script setup lang="ts">
import type { NavigationMenuItem } from '@nuxt/ui'

const { apiReferenceUrl } = useRuntimeConfig().public
const { t } = useI18n()

// One tab per settings group. Adding a group to `settingsGroups` (and a page under
// `pages/settings/`) is all it takes to get another — nothing here enumerates them.
const links = computed(() => [
  settingsGroups.map(group => ({
    label: groupLabel(group),
    icon: group.icon,
    to: group.to,
    exact: group.to === '/settings'
  })),
  [{
    label: t('nav.apiReference'),
    icon: 'i-lucide-book-open',
    to: apiReferenceUrl,
    target: '_blank'
  }]
] satisfies NavigationMenuItem[][])

useHead({ title: () => t('nav.documentTitle', { page: t('nav.settings') }) })
</script>

<template>
  <UDashboardPanel id="settings" :ui="{ body: 'lg:py-12' }">
    <template #header>
      <UDashboardNavbar :title="$t('nav.settings')">
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
