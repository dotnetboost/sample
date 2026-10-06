import type { SettingGroup } from '~/types'

/**
 * The settings groups the dashboard exposes, one tab each.
 *
 * `route` must match the `[SettingGroup("…")]` attribute on the C# class — that is where
 * `MapSettingsEndpoints()` mounts the group. Everything else is presentation: the form fields come
 * from whatever the API returns, so a property added to the C# class appears without an entry
 * below, and a property removed from it disappears.
 *
 * That last part is visible here: `BrandingSettings` used to carry a `LogoFileId`, and when it was
 * removed the field stopped rendering on its own. The logo is a media attachment now, which the
 * branding tab draws beneath the form.
 *
 * **What is not here any more.** The labels and the help text used to be; they live in the message
 * bundles under `settings.groups.{route}`, addressed by `groupLabel`/`fieldLabel` below. And the
 * numeric bounds used to be — `headerLogoSize: { min: 16, max: 64 }` and
 * `pageSize: { min: 1, max: 200 }`, two ranges that appeared nowhere in C# and that the API would
 * happily have ignored. `GET /api/settings/{route}/schema` publishes them now, from the
 * FluentValidation rules in `Settings/SettingsValidators.cs`, along with each property's type, its
 * default and whether it is `[Sensitive]`. `GroupForm` reads that.
 *
 * So what is left per group is the nav — a route, the C# class name, an icon, a page to open — and
 * the one field flag a server cannot supply: `translated`, which mirrors `[MultiLingual]`.
 * DotNetBoost.Settings has never heard of that attribute; it belongs to another library, and the
 * settings engine does not know it exists. Which is the sample's whole subject in miniature.
 */
export const settingsGroups: SettingGroup[] = [
  {
    route: 'branding',
    name: 'BrandingSettings',
    icon: 'i-lucide-palette',
    to: '/settings',
    // Only `[MultiLingual]`, which the schema endpoint cannot know about. Everything else about
    // these properties — including the 16–64 range this used to assert — comes from the API.
    fields: {
      storeName: { translated: true }
    }
  },
  {
    route: 'catalog',
    name: 'CatalogSettings',
    icon: 'i-lucide-package',
    to: '/settings/catalog',
    // Nothing. Every one of this group's properties is described by the API, which is what the
    // schema endpoint was for.
    fields: {}
  }
]

export function findSettingsGroup(route: string): SettingGroup | undefined {
  return settingsGroups.find(group => group.route === route)
}

/**
 * A group's and a property's visible text, addressed in the bundles by route and property name.
 *
 * Built at runtime rather than written out, because the key does not exist until the group does —
 * which is the same reason the form's fields come from the API's response rather than from a list
 * here. A property the bundles say nothing about still renders: it falls back to `humanizeKey`,
 * so adding one to the C# class puts a usable field on the form before anyone writes it a label.
 */
export function groupLabel(group: SettingGroup): string {
  return uiText(`settings.groups.${group.route}.label`, group.name)
}

export function groupDescription(group: SettingGroup): string {
  return uiText(`settings.groups.${group.route}.description`, '')
}

export function fieldLabel(group: SettingGroup, key: string): string {
  return uiText(`settings.groups.${group.route}.fields.${key}.label`, humanizeKey(key))
}

export function fieldDescription(group: SettingGroup, key: string): string {
  return uiText(`settings.groups.${group.route}.fields.${key}.description`, '')
}

/** `useSsl` -> `Use Ssl`. Fallback label for properties the bundles do not name. */
export function humanizeKey(key: string): string {
  const spaced = key.replace(/([a-z0-9])([A-Z])/g, '$1 $2')
  return spaced.charAt(0).toUpperCase() + spaced.slice(1)
}
