<script setup lang="ts">
import { BookOpen, ExternalLink } from '@lucide/vue'

const { apiReferenceUrl } = useRuntimeConfig().public
const { t } = useI18n()
const route = useRoute()

// One tab per registered [SettingGroup], discovered from GET /api/settings. Add a settings class
// in C# and a tab appears here with no change in this client: it gets the default icon and a form
// generated from its own schema. Nothing in the dashboard enumerates them any more.
const { data: groups } = await useSettingGroups()

const links = computed(() => (groups.value ?? []).map(group => ({
  label: groupLabel(group.route, group.name),
  icon: group.icon,
  to: groupPath(group.route)
})))

useHead({ title: () => t('nav.documentTitle', { page: t('nav.settings') }) })
</script>

<template>
  <DashboardPage :title="$t('nav.settings')">
    <template #toolbar>
      <!-- Underlined tabs, as links: each group is its own address, so it can be linked to and
           reloaded, and the browser's back button walks between them. -->
      <nav class="-mb-2 flex flex-1 items-center gap-1 overflow-x-auto">
        <NuxtLink
          v-for="link in links"
          :key="link.to"
          :to="link.to"
          class="inline-flex shrink-0 items-center gap-2 border-b-2 px-3 pt-1.5 pb-2.5 text-sm font-medium transition-colors"
          :class="isCurrentPath(route.path, link.to)
            ? 'border-primary text-foreground'
            : 'border-transparent text-muted-foreground hover:text-foreground'"
        >
          <component :is="link.icon" class="size-4" />
          {{ link.label }}
        </NuxtLink>

        <a
          :href="apiReferenceUrl"
          target="_blank"
          rel="noopener noreferrer"
          class="ms-auto inline-flex shrink-0 items-center gap-2 border-b-2 border-transparent px-3 pt-1.5 pb-2.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
        >
          <BookOpen class="size-4" />
          {{ $t('nav.apiReference') }}
          <ExternalLink class="size-3 opacity-50" />
        </a>
      </nav>
    </template>

    <div class="mx-auto flex w-full flex-col gap-4 sm:gap-6 lg:max-w-3xl lg:py-4">
      <NuxtPage />
    </div>
  </DashboardPage>
</template>
