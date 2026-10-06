import type { SettingGroup } from '~/types'

/** The key every settings page shares, so the tabs and the open page are one fetch. */
export const SETTING_GROUPS_KEY = 'setting-groups'

/**
 * The registered settings groups, discovered from `GET /api/settings`.
 *
 * This is what makes "a tab per `[SettingGroup]`" true rather than aspirational: the API names the
 * classes it found, and this dashboard draws a tab for each. Adding a settings class in C# needs no
 * change in the client — it gets the default icon and a form generated from its own schema.
 *
 * The response carries no values, only routes and names, so it is safe to fetch for the navigation
 * of a page the reader may not open. Each group's values come from its own endpoint when its tab
 * is.
 *
 * The only thing added on the way through is an icon, from `utils/settings.ts`, with a default for
 * a group this dashboard has never heard of. Everything about a group's properties — including
 * `translated`, which the settings engine cannot know about on its own — comes from that group's
 * schema.
 */
export function useSettingGroups() {
  const api = useApi()

  return useAsyncData<SettingGroup[]>(SETTING_GROUPS_KEY, async () => {
    const descriptors = await api.settings.groups()

    return descriptors.map(descriptor => ({
      route: descriptor.route,
      name: descriptor.name,
      requiresAuthorization: descriptor.requiresAuthorization,
      icon: groupIcon(descriptor.route)
    }))
  })
}
