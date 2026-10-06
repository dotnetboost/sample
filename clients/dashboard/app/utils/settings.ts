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
 * **What is not here any more.** The labels and the help text used to be. They live in the message
 * bundles now, under `settings.groups.{route}`, and `groupLabel`/`fieldLabel` below address them
 * by route and property name. So this file holds only what a translator has no business editing —
 * the route, the C# class name, the icon, and the numeric bounds a control needs — and adding a
 * group means adding its text in three bundles rather than one unlocalised string here.
 */
export const settingsGroups: SettingGroup[] = [
  {
    route: 'branding',
    name: 'BrandingSettings',
    icon: 'i-lucide-palette',
    to: '/settings',
    fields: {
      storeName: { translated: true },
      accentColor: {},
      headerLogoSize: { min: 16, max: 64, step: 1 }
    }
  },
  {
    route: 'catalog',
    name: 'CatalogSettings',
    icon: 'i-lucide-package',
    to: '/settings/catalog',
    fields: {
      pageSize: { min: 1, max: 200, step: 1 },
      currencySymbol: {}
    }
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
