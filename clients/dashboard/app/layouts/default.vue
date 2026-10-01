<script setup lang="ts">
const api = useApi()
const language = useLanguage()

const { data: languages } = await useAsyncData('languages', () => api.multiLingual.languages())
const { data: branding } = await useAsyncData('branding', () => api.branding.get())

const config = useRuntimeConfig()

const links = [
  { label: 'Overview', icon: 'i-lucide-layout-dashboard', to: '/' },
  { label: 'Catalog', icon: 'i-lucide-package', to: '/catalog' },
  { label: 'Media & alt text', icon: 'i-lucide-image', to: '/media' },
  { label: 'Languages', icon: 'i-lucide-languages', to: '/languages' },
  { label: 'Settings', icon: 'i-lucide-settings', to: '/settings' }
]

// Arabic is right-to-left; the whole shell flips, which is the point of storing direction
// alongside the language rather than hardcoding a list of RTL codes in the client.
const direction = computed(() => {
  const current = languages.value?.find(l => l.code === language.value)
  return isRtl(current?.direction) ? 'rtl' : 'ltr'
})

const languageItems = computed(() =>
  (languages.value ?? []).filter(l => l.isActive).map(l => ({
    label: l.name,
    value: l.code
  }))
)
</script>

<template>
  <div :dir="direction" class="min-h-screen bg-default">
    <header class="border-b border-default">
      <div class="mx-auto flex max-w-7xl items-center gap-4 px-4 py-3">
        <div class="flex items-center gap-3">
          <img
            v-if="branding?.logo"
            :src="branding.logo.url"
            :alt="branding.storeName"
            class="size-8 rounded object-cover"
          >
          <UIcon v-else name="i-lucide-boxes" class="size-7 text-primary" />
          <div>
            <p class="font-semibold leading-tight">
              {{ branding?.storeName ?? 'DotNetBoost Sample' }}
            </p>
            <p class="text-xs text-muted">
              Settings · Media · MultiLingual
            </p>
          </div>
        </div>

        <div class="flex-1" />

        <USelect
          v-model="language"
          :items="languageItems"
          value-key="value"
          icon="i-lucide-languages"
          class="w-40"
        />

        <UButton
          :to="config.public.apiReferenceUrl"
          target="_blank"
          icon="i-lucide-book-open"
          variant="ghost"
          color="neutral"
          label="API"
        />
      </div>

      <nav class="mx-auto max-w-7xl px-4">
        <UNavigationMenu :items="links" />
      </nav>
    </header>

    <main class="mx-auto max-w-7xl px-4 py-6">
      <slot />
    </main>
  </div>
</template>
