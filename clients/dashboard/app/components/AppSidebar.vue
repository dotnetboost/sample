<script setup lang="ts">
import type { SidebarProps } from '~/components/ui/sidebar'
import { ExternalLink, Search } from '@lucide/vue'
import { useSidebar } from '~/components/ui/sidebar'

/**
 * The dashboard's sidebar, after shadcn-vue's `dashboard-01` and `sidebar-07` blocks: the store's
 * brand on top, the pages, the links out pushed to the bottom, and the appearance menu in the
 * footer where the blocks keep the signed-in user. It collapses to icons rather than off-canvas,
 * because six pages are few enough to recognise by icon alone.
 *
 * It sits on the reading side in Arabic without being told: the desktop sidebar is positioned with
 * logical properties, and `ui/sidebar/Sidebar.vue` flips the mobile sheet, whose side is physical.
 */
const props = withDefaults(defineProps<SidebarProps>(), {
  collapsible: 'icon',
  variant: 'inset'
})

const route = useRoute()
const { main, resources } = useNavigation()
const { isMobile, setOpenMobile } = useSidebar()

const searchOpen = useCommandPalette()

// A link followed from the mobile sheet should not leave the sheet covering the page it opened.
function followed() {
  if (isMobile.value) setOpenMobile(false)
}
</script>

<template>
  <Sidebar v-bind="props">
    <SidebarHeader>
      <AppBrand />
    </SidebarHeader>

    <SidebarContent>
      <SidebarGroup>
        <SidebarGroupContent>
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton
                :tooltip="$t('nav.search')"
                class="text-muted-foreground"
                @click="searchOpen = true"
              >
                <Search />
                <span>{{ $t('nav.search') }}</span>
                <Kbd class="ms-auto group-data-[collapsible=icon]:hidden">⌘K</Kbd>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarGroupContent>
      </SidebarGroup>

      <SidebarGroup>
        <SidebarGroupLabel>{{ $t('nav.main') }}</SidebarGroupLabel>
        <SidebarGroupContent>
          <SidebarMenu>
            <SidebarMenuItem v-for="link in main" :key="link.to">
              <SidebarMenuButton
                as-child
                :tooltip="link.label"
                :is-active="isCurrentPath(route.path, link.to)"
              >
                <NuxtLink :to="link.to" @click="followed">
                  <component :is="link.icon" />
                  <span>{{ link.label }}</span>
                </NuxtLink>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarGroupContent>
      </SidebarGroup>

      <SidebarGroup class="mt-auto">
        <SidebarGroupLabel>{{ $t('nav.resources') }}</SidebarGroupLabel>
        <SidebarGroupContent>
          <SidebarMenu>
            <SidebarMenuItem v-for="link in resources" :key="link.to">
              <SidebarMenuButton as-child size="sm" :tooltip="link.label">
                <a :href="link.to" target="_blank" rel="noopener noreferrer">
                  <component :is="link.icon" />
                  <span>{{ link.label }}</span>
                  <ExternalLink class="ms-auto opacity-50" />
                </a>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarGroupContent>
      </SidebarGroup>
    </SidebarContent>

    <SidebarFooter>
      <AppearanceMenu />
    </SidebarFooter>

    <SidebarRail />
  </Sidebar>
</template>
