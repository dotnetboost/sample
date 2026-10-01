<script setup lang="ts">
import type { NavigationMenuItem } from '@nuxt/ui'

const { apiReferenceUrl } = useRuntimeConfig().public

const api = useApi()
const language = useLanguage()

const { data: languages } = await useAsyncData('layout-languages', () => api.multiLingual.languages())

const open = ref(false)
const close = () => {
  open.value = false
}

const links = [[{
  label: 'Catalog',
  icon: 'i-lucide-package',
  to: '/catalog',
  onSelect: close
}, {
  label: 'Media',
  icon: 'i-lucide-images',
  to: '/media',
  onSelect: close
}, {
  label: 'Languages',
  icon: 'i-lucide-languages',
  to: '/languages',
  onSelect: close
}, {
  label: 'Settings',
  icon: 'i-lucide-settings',
  to: '/settings',
  onSelect: close
}], [{
  label: 'API reference',
  icon: 'i-lucide-book-open',
  to: apiReferenceUrl,
  target: '_blank'
}, {
  label: 'DotNetBoost on GitHub',
  icon: 'i-simple-icons-github',
  to: 'https://github.com/dotnetboost',
  target: '_blank'
}]] satisfies NavigationMenuItem[][]

const groups = computed(() => [{
  id: 'links',
  label: 'Go to',
  items: links.flat()
}])

// The whole shell flips for a right-to-left language — which is why direction is stored
// alongside the language rather than hardcoded as a list of RTL codes in the client.
const direction = computed(() =>
  isRtl(languages.value?.find(l => l.code === language.value)?.direction) ? 'rtl' : 'ltr')
</script>

<template>
  <div :dir="direction">
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
          <AppearanceMenu :collapsed="collapsed" />
        </template>
      </UDashboardSidebar>

      <UDashboardSearch :groups="groups" />

      <slot />
    </UDashboardGroup>
  </div>
</template>
