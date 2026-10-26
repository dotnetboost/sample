import type { Component } from 'vue'
import { BookOpen, History, Images, Languages, LayoutDashboard, Package, Settings } from '@lucide/vue'
import GithubIcon from '~/components/GithubIcon.vue'

export interface NavLink {
  label: string
  icon: Component
  to: string
  /** Set for a link that leaves the dashboard — it opens in a new tab. */
  external?: boolean
}

/**
 * The dashboard's pages and the two places it links out to, as the sidebar and the command palette
 * both draw them.
 *
 * One list for both, so a page added here is reachable from either, and the two cannot disagree on
 * what a page is called. Computed rather than a constant: the labels have to be rebuilt when the
 * language changes, and a plain array would hold whatever language the sidebar was first rendered
 * in.
 */
export function useNavigation() {
  const { apiReferenceUrl } = useRuntimeConfig().public
  const { t } = useI18n()

  const main = computed<NavLink[]>(() => [
    { label: t('nav.overview'), icon: LayoutDashboard, to: '/' },
    { label: t('nav.catalog'), icon: Package, to: '/catalog' },
    { label: t('nav.media'), icon: Images, to: '/media' },
    { label: t('nav.languages'), icon: Languages, to: '/languages' },
    { label: t('nav.audit'), icon: History, to: '/audit' },
    { label: t('nav.settings'), icon: Settings, to: '/settings' }
  ])

  const resources = computed<NavLink[]>(() => [
    { label: t('nav.apiReference'), icon: BookOpen, to: apiReferenceUrl, external: true },
    { label: t('nav.github'), icon: GithubIcon, to: 'https://github.com/dotnetboost', external: true }
  ])

  return { main, resources }
}

/**
 * Whether `to` is the page being read — or, for a section with pages of its own (`/settings/…`),
 * one of them. `/` only ever matches itself, or every page would light up the overview.
 */
export function isCurrentPath(path: string, to: string) {
  return to === '/' ? path === '/' : path === to || path.startsWith(`${to}/`)
}
