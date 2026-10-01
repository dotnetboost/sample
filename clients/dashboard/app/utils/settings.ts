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
 */
export const settingsGroups: SettingGroup[] = [
  {
    route: 'branding',
    name: 'BrandingSettings',
    label: 'Branding',
    description: 'How the store presents itself. Saved changes take effect on the next read — no restart, no redeploy.',
    icon: 'i-lucide-palette',
    to: '/settings',
    fields: {
      storeName: { label: 'Store name', description: 'Shown in the sidebar header, in place of the sample\'s own name.' },
      accentColor: { label: 'Accent colour', description: 'A CSS hex value, e.g. #512BD4.' },
      headerLogoSize: {
        label: 'Header logo size',
        description: 'How large the logo is drawn in the sidebar header, in pixels. The header clamps what it renders to 16–64.',
        min: 16,
        max: 64,
        step: 1
      }
    }
  },
  {
    route: 'catalog',
    name: 'CatalogSettings',
    label: 'Catalog',
    description: 'How the catalogue is presented. These are read on every listing, so a change is visible immediately.',
    icon: 'i-lucide-package',
    to: '/settings/catalog',
    fields: {
      pageSize: { label: 'Page size', description: 'Products returned per page.', min: 1, max: 200, step: 1 },
      currencySymbol: { label: 'Currency symbol', description: 'Shown next to prices.' },
      showProductsWithoutImages: {
        label: 'List products without images',
        description: 'When off, a product with no attached image is left out of listings.'
      }
    }
  }
]

export function findSettingsGroup(route: string): SettingGroup | undefined {
  return settingsGroups.find(group => group.route === route)
}

/** `useSsl` -> `Use Ssl`. Fallback label for properties with no metadata. */
export function humanizeKey(key: string): string {
  const spaced = key.replace(/([a-z0-9])([A-Z])/g, '$1 $2')
  return spaced.charAt(0).toUpperCase() + spaced.slice(1)
}
