<script setup lang="ts">
const { t } = useI18n()

useHead({ title: () => t('nav.documentTitle', { page: t('nav.media') }) })
</script>

<template>
  <DashboardPage :title="$t('nav.media')">
    <SectionIntro :title="$t('media.page.title')" :description="$t('media.page.description')" />

    <MediaBrowser :page-size="12" />
  </DashboardPage>
</template>
