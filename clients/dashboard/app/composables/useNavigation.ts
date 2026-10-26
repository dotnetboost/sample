import type { Component } from 'vue'
import type { RouteLocationRaw } from 'vue-router'
import { BookOpen, History, Images, Languages, LayoutDashboard, Package, Settings } from '@lucide/vue'
import GithubIcon from '~/components/GithubIcon.vue'

export interface NavItem {
  label: string
  to: RouteLocationRaw
  /** Whether this is the page being read. Worked out here, where each item knows its own address. */
  active: boolean
}

export interface NavLink extends NavItem {
  /** Stable across languages, unlike the label — what the sidebar remembers a section's state by. */
  id: string
  icon: Component
  /** The page's own sections, shown nested under it — Catalog's two tabs, Settings' groups. */
  children?: NavItem[]
}

export interface NavGroup {
  /** Empty for the group at the top, which stands without a heading — the Overview, as in dashboard-01. */
  label: string
  links: NavLink[]
}

export interface ExternalLink {
  label: string
  icon: Component
  href: string
}

/**
 * The dashboard's pages, grouped by what they are for, as the sidebar and the command palette both
 * draw them.
 *
 *   • the Overview on its own, at the top;
 *   • **Content** — what the store sells and the files it shows: the catalogue first, because it
 *     is the daily work, and the media library that serves it;
 *   • **Administration** — how the store is set up, then the record of what changed: languages
 *     first, because they shape the content above, and the audit trail last, because it is
 *     read-only review.
 *
 * Catalog and Settings carry their sections as children, in the `sidebar-07` block's nested menu.
 * Settings' come from `GET /api/settings` through `useSettingGroups`, so a `[SettingGroup]` added
 * in C# appears here as well as on the Settings page, with nothing to change in this file.
 *
 * One list for both readers, so a page added here is reachable from either, and the two cannot
 * disagree on what a page is called. Computed rather than a constant: the labels have to be
 * rebuilt when the language changes, and so does `active` when the route does.
 */
export function useNavigation() {
  const { apiReferenceUrl } = useRuntimeConfig().public
  const { t } = useI18n()
  const route = useRoute()

  // Not awaited: the sidebar draws before the groups land, and gains Settings' children when they
  // do. The Settings page itself reads the same entry, so this costs no request of its own.
  const { data: settingGroups } = useSettingGroups()

  const at = (path: string) => isCurrentPath(route.path, path)

  const groups = computed<NavGroup[]>(() => {
    const catalogTab = route.query.tab === 'products' ? 'products' : 'categories'

    return [{
      label: '',
      links: [
        { id: 'overview', label: t('nav.overview'), icon: LayoutDashboard, to: '/', active: at('/') }
      ]
    }, {
      label: t('nav.content'),
      links: [{
        id: 'catalog',
        label: t('nav.catalog'),
        icon: Package,
        to: '/catalog',
        active: at('/catalog'),
        children: [{
          label: t('catalog.tabs.categories'),
          to: '/catalog',
          active: at('/catalog') && catalogTab === 'categories'
        }, {
          label: t('catalog.tabs.products'),
          to: { path: '/catalog', query: { tab: 'products' } },
          active: at('/catalog') && catalogTab === 'products'
        }]
      }, {
        id: 'media', label: t('nav.media'), icon: Images, to: '/media', active: at('/media')
      }]
    }, {
      label: t('nav.administration'),
      links: [{
        id: 'languages', label: t('nav.languages'), icon: Languages, to: '/languages', active: at('/languages')
      }, {
        id: 'settings',
        label: t('nav.settings'),
        icon: Settings,
        to: '/settings',
        active: at('/settings'),
        children: (settingGroups.value ?? []).map(group => ({
          label: groupLabel(group.route, group.name),
          to: groupPath(group.route),
          active: at(groupPath(group.route))
        }))
      }, {
        id: 'audit', label: t('nav.audit'), icon: History, to: '/audit', active: at('/audit')
      }]
    }]
  })

  const resources = computed<ExternalLink[]>(() => [
    { label: t('nav.apiReference'), icon: BookOpen, href: apiReferenceUrl },
    { label: t('nav.github'), icon: GithubIcon, href: 'https://github.com/dotnetboost' }
  ])

  return { groups, resources }
}

/**
 * Whether `to` is the page being read — or, for a section with pages of its own (`/settings/…`),
 * one of them. `/` only ever matches itself, or every page would light up the overview.
 */
export function isCurrentPath(path: string, to: string) {
  return to === '/' ? path === '/' : path === to || path.startsWith(`${to}/`)
}
