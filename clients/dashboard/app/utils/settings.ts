import type { Component } from 'vue'
import { History, Package, Palette, SlidersHorizontal } from '@lucide/vue'

/**
 * The client's half of a settings tab: an icon, and the one field hint no server can supply.
 *
 * **What is not here is the point.** This used to be the list of settings groups — the source of
 * which tabs exist, and of every field's bounds. Both come from the API now:
 *
 *   • `GET /api/settings` names the registered `[SettingGroup]` classes, so adding one in C# puts
 *     a tab on this page with no change here. See `useSettingGroups`.
 *   • `GET /api/settings/{route}/schema` describes each property — its type, its default, whether
 *     it is `[Sensitive]`, and the constraints its validators enforce. `headerLogoSize`'s 16–64
 *     range used to be a literal in this file that matched nothing in C#, and that the API would
 *     have accepted any value outside.
 *
 * `translated` used to be here too — the flag mirroring `[MultiLingual]`, which
 * DotNetBoost.Settings cannot publish because the attribute belongs to another library. The API
 * publishes it now, through this application's own `MultiLingualSchemaContributor`, merged with
 * the validation rules rather than replacing them.
 *
 * So what is left is one thing a server genuinely cannot answer: which icon to draw. A group with
 * no entry below still works — it gets the default icon and a form generated entirely from its own
 * schema, which is what makes discovery real rather than decorative.
 */
const icons: Record<string, Component> = {
  auditing: History,
  branding: Palette,
  catalog: Package
}

/** Shown for a group this dashboard has no icon for — a class added in C# and nowhere else. */
const defaultIcon = SlidersHorizontal

export function groupIcon(route: string): Component {
  return icons[route] ?? defaultIcon
}

/** Where a group's tab goes. Derived, so a discovered group needs no entry to be reachable. */
export function groupPath(route: string): string {
  return `/settings/${route}`
}

/**
 * A group's and a property's visible text, addressed in the bundles by route and property name.
 *
 * Addressed by route rather than by a group object, because the group now arrives from the API and
 * the bundles are keyed on something stable. A property the bundles say nothing about still
 * renders: it falls back to `humanizeKey`, so a property added to the C# class puts a usable field
 * on the form before anyone writes it a label.
 */
export function groupLabel(route: string, fallback = ''): string {
  return uiText(`settings.groups.${route}.label`, fallback || humanizeKey(route))
}

export function groupDescription(route: string): string {
  return uiText(`settings.groups.${route}.description`, '')
}

export function fieldLabel(route: string, key: string): string {
  return uiText(`settings.groups.${route}.fields.${key}.label`, humanizeKey(key))
}

export function fieldDescription(route: string, key: string): string {
  return uiText(`settings.groups.${route}.fields.${key}.description`, '')
}

/** `useSsl` -> `Use Ssl`. Fallback label for properties the bundles do not name. */
export function humanizeKey(key: string): string {
  const spaced = key.replace(/([a-z0-9])([A-Z])/g, '$1 $2')
  return spaced.charAt(0).toUpperCase() + spaced.slice(1)
}
