<script setup lang="ts">
import { BookOpen, ExternalLink } from '@lucide/vue'

const { apiReferenceUrl } = useRuntimeConfig().public
const { t } = useI18n()
const route = useRoute()

// One entry per registered [SettingGroup], discovered from GET /api/settings. Add a settings class
// in C# and an entry appears here with no change in this client: it gets the default icon and a form
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
    <!-- The API's own reference, at the top of the page as it was over the old tab strip: it is
         about every group at once, so it belongs to the page rather than to the list of them. -->
    <template #actions>
      <Button as-child variant="outline" size="sm">
        <a
          :href="apiReferenceUrl"
          target="_blank"
          rel="noopener noreferrer"
          :aria-label="$t('nav.apiReference')"
        >
          <BookOpen />
          <span class="max-sm:sr-only">{{ $t('nav.apiReference') }}</span>
          <ExternalLink class="opacity-50 max-sm:hidden" />
        </a>
      </Button>
    </template>

    <!--
      The groups down the start side, the open group's form beside them — the layout of
      shadcn-vue's own settings example, rather than a tab strip over the page: a list of groups
      can grow without running off the end of a row, and each one still reads as a place.

      "Start" rather than "left", so in Arabic the list is on the right. On a phone there is no
      room beside the form, so the list goes above it.
    -->
    <div class="flex flex-col gap-6 md:flex-row lg:gap-10">
      <!--
        At the top of the start side, level with the form's heading. The column is as tall as the
        form, so from `lg` up the list can stick under the page header and stay in reach while a
        long form scrolls past it.
      -->
      <div class="shrink-0 md:w-56 md:self-stretch">
        <!-- Links, not buttons: each group is its own address, so it can be linked to and
             reloaded, and the browser's back button walks between them. Each its own card, the
             open one tinted with the accent and marked by a bar down its start edge. -->
        <nav
          :aria-label="$t('nav.settings')"
          class="flex flex-col gap-2 lg:sticky lg:top-[calc(var(--page-sticky-top)+1.5rem)]"
        >
          <NuxtLink
            v-for="link in links"
            :key="link.to"
            :to="link.to"
            class="relative flex items-center gap-2.5 overflow-hidden rounded-lg border bg-card px-3 py-2.5 text-sm font-medium shadow-xs transition-colors"
            :class="isCurrentPath(route.path, link.to)
              ? 'border-primary/40 bg-primary/5 text-foreground before:absolute before:inset-y-0 before:start-0 before:w-1 before:bg-primary'
              : 'text-muted-foreground hover:border-foreground/20 hover:bg-muted/40 hover:text-foreground'"
            :aria-current="isCurrentPath(route.path, link.to) ? 'page' : undefined"
          >
            <!-- The icon in a small square of its own, so the column of them lines up like a
                 menu's rather than floating at the text's baseline. -->
            <span
              class="flex size-7 shrink-0 items-center justify-center rounded-md border bg-background"
              :class="isCurrentPath(route.path, link.to) ? 'text-primary' : 'text-muted-foreground'"
            >
              <component :is="link.icon" class="size-4" />
            </span>
            <span class="truncate">{{ link.label }}</span>
          </NuxtLink>
        </nav>
      </div>

      <div class="flex min-w-0 flex-1 flex-col gap-4 sm:gap-6 lg:max-w-3xl">
        <NuxtPage />
      </div>
    </div>
  </DashboardPage>
</template>
