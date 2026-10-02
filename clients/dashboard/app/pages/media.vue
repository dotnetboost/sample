<script setup lang="ts">
const { t } = useI18n()

useHead({ title: () => t('nav.documentTitle', { page: t('nav.media') }) })
</script>

<template>
  <UDashboardPanel id="media">
    <template #header>
      <UDashboardNavbar :title="$t('nav.media')">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>

        <template #right>
          <LanguageSelect />
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <UPageCard
        :title="$t('media.page.title')"
        :description="$t('media.page.description')"
        variant="naked"
      />

      <MediaBrowser :page-size="12" />
    </template>
  </UDashboardPanel>
</template>
