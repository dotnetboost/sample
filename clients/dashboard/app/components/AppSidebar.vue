<script setup lang="ts">
import type { SidebarProps } from '~/components/ui/sidebar'
import { ChevronRight, ExternalLink, Search } from '@lucide/vue'
import { useSidebar } from '~/components/ui/sidebar'

/**
 * The dashboard's sidebar, after shadcn-vue's `dashboard-01` and `sidebar-07` blocks: the store's
 * brand on top, the pages in groups by what they are for (see `useNavigation`), the links out
 * pushed to the bottom, and the appearance menu in the footer where the blocks keep the signed-in
 * user. It collapses to icons rather than off-canvas, because six pages are few enough to
 * recognise by icon alone.
 *
 * A page with sections — Catalog, Settings — unfolds them in the `sidebar-08` block's way rather
 * than `sidebar-07`'s: the page's own row stays a link, and a separate chevron unfolds it. In
 * `sidebar-07` the whole row is the toggle, which collapsed to icons leaves a button that opens
 * nothing; here the icon still goes to the page. A section unfolds whenever the page being read
 * is in it, and otherwise stays as the reader left it.
 *
 * It sits on the reading side in Arabic without being told: the desktop sidebar is positioned with
 * logical properties, and `ui/sidebar/Sidebar.vue` flips the mobile sheet, whose side is physical.
 */
const props = withDefaults(defineProps<SidebarProps>(), {
  collapsible: 'icon',
  variant: 'inset'
})

const { groups, resources } = useNavigation()

/**
 * Which sections are unfolded, by link id. Opened whenever its page becomes the one being read —
 * not just at first render, or following a link into Settings from the Overview would leave its
 * groups folded away — and otherwise left as the reader set it.
 */
const unfolded = reactive<Record<string, boolean>>({})

watchEffect(() => {
  for (const link of groups.value.flatMap(group => group.links)) {
    if (link.active && link.children?.length) unfolded[link.id] = true
  }
})
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

      <SidebarGroup v-for="group in groups" :key="group.label">
        <SidebarGroupLabel v-if="group.label">
          {{ group.label }}
        </SidebarGroupLabel>
        <SidebarGroupContent>
          <SidebarMenu>
            <Collapsible
              v-for="link in group.links"
              :key="link.id"
              v-model:open="unfolded[link.id]"
              as-child
            >
              <SidebarMenuItem>
                <SidebarMenuButton as-child :tooltip="link.label" :is-active="link.active">
                  <NuxtLink :to="link.to" @click="followed">
                    <component :is="link.icon" />
                    <span>{{ link.label }}</span>
                  </NuxtLink>
                </SidebarMenuButton>

                <template v-if="link.children?.length">
                  <CollapsibleTrigger as-child>
                    <!-- Mirrored in Arabic, where "unfold" points the other way. -->
                    <SidebarMenuAction
                      class="data-[state=open]:rotate-90 rtl:data-[state=closed]:rotate-180"
                      :aria-label="$t('nav.showSections', { page: link.label })"
                    >
                      <ChevronRight />
                    </SidebarMenuAction>
                  </CollapsibleTrigger>
                  <CollapsibleContent>
                    <SidebarMenuSub>
                      <SidebarMenuSubItem v-for="child in link.children" :key="child.label">
                        <SidebarMenuSubButton as-child :is-active="child.active">
                          <NuxtLink :to="child.to" @click="followed">
                            <span>{{ child.label }}</span>
                          </NuxtLink>
                        </SidebarMenuSubButton>
                      </SidebarMenuSubItem>
                    </SidebarMenuSub>
                  </CollapsibleContent>
                </template>
              </SidebarMenuItem>
            </Collapsible>
          </SidebarMenu>
        </SidebarGroupContent>
      </SidebarGroup>

      <SidebarGroup class="mt-auto">
        <SidebarGroupLabel>{{ $t('nav.resources') }}</SidebarGroupLabel>
        <SidebarGroupContent>
          <SidebarMenu>
            <SidebarMenuItem v-for="link in resources" :key="link.href">
              <SidebarMenuButton as-child size="sm" :tooltip="link.label">
                <a :href="link.href" target="_blank" rel="noopener noreferrer">
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
