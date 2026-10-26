<script setup lang="ts">
import { House, SearchX } from '@lucide/vue'
import type { NuxtError } from '#app'

defineProps<{
  error: NuxtError
}>()

const { t, locale } = useI18n()

useSeoMeta({
  title: () => t('errorPage.title'),
  description: () => t('errorPage.description')
})

// The error page replaces the whole app, so `app.vue` is not mounted to set these. It has no
// language list to ask for a direction either, so it takes the UI locale's own — which is the
// best it can know, and right for the three bundles that exist.
useHead({
  htmlAttrs: {
    lang: locale,
    dir: () => (locale.value === 'ar' ? 'rtl' : 'ltr')
  }
})
</script>

<template>
  <div class="flex min-h-svh items-center justify-center bg-background p-6">
    <Empty>
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <SearchX />
        </EmptyMedia>
        <p class="font-mono text-sm text-muted-foreground">
          {{ error.statusCode }}
        </p>
        <EmptyTitle>{{ $t('errorPage.title') }}</EmptyTitle>
        <EmptyDescription>{{ $t('errorPage.description') }}</EmptyDescription>
      </EmptyHeader>
      <EmptyContent>
        <Button @click="clearError({ redirect: '/' })">
          <House />
          {{ $t('nav.overview') }}
        </Button>
      </EmptyContent>
    </Empty>
  </div>
</template>
