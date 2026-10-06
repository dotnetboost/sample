<script setup lang="ts">
import type { NavigationMenuItem } from '@nuxt/ui'

const { apiReferenceUrl } = useRuntimeConfig().public
const { t } = useI18n()

// One tab per registered [SettingGroup], discovered from GET /api/settings. Add a settings class
// in C# and a tab appears here with no change in this client: it gets the default icon and a form
// generated from its own schema. Nothing in the dashboard enumerates them any more.
const { data: groups } = await useSettingGroups()

const links = computed(() => [
  (groups.value ?? []).map(group => ({
    label: groupLabel(group.route, group.name),
    icon: group.icon,
    to: groupPath(group.route)
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
