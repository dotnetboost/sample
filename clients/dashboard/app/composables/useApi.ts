/**
 * Every call the dashboard makes, in one place — grouped by which library answers it.
 *
 * Worth reading as documentation of the composition: the `media` and `multiLingual` groups talk to
 * two libraries that know nothing about each other, and `mediaText` talks to the application's own
 * endpoint that joins them.
 */
export interface Language {
  code: string
  name: string
  direction: number | string
  isDefault: boolean
  isActive: boolean
}

export interface MediaTextValues {
  altText: string | null
  caption: string | null
}

export interface ProductImage {
  id: string
  fileName: string
  contentType: string
  size: number
  altText: string | null
  caption: string | null
  url: string
}

export interface Product {
  id: number
  name: string
  description: string
  sku: string
  price: number
  categoryId: number
  images: ProductImage[]
}

export interface Category {
  id: number
  name: string
  description: string
  slug: string
  products: number
}

/** The language the dashboard is reading in. Sent as Accept-Language on every call. */
export const useLanguage = () => useState<string>('language', () => 'en')

export function useApi() {
  const language = useLanguage()

  const headers = () => ({ 'Accept-Language': language.value })

  return {
    // ── DotNetBoost.MultiLingual ──────────────────────────────────────────
    multiLingual: {
      languages: () => $fetch<Language[]>('/api/languages'),
      create: (body: { code: string, name: string, direction?: string }) =>
        $fetch('/api/languages', { method: 'POST', body })
    },

    // ── DotNetBoost.Media ─────────────────────────────────────────────────
    media: {
      list: () => $fetch<{ items: ProductImage[], total: number }>('/api/media'),
      remove: (id: string) => $fetch(`/api/media/${id}`, { method: 'DELETE' })
    },

    // ── DotNetBoost.Settings ──────────────────────────────────────────────
    settings: {
      get: <T>(group: string) => $fetch<T>(`/api/settings/${group}`),
      set: <T>(group: string, body: T) =>
        $fetch(`/api/settings/${group}`, { method: 'POST', body })
    },

    // ── The application's own endpoints ───────────────────────────────────
    catalog: {
      categories: () => $fetch<Category[]>('/api/categories', { headers: headers() }),
      products: () => $fetch<Product[]>('/api/products', { headers: headers() }),
      uploadImage: (productId: number, file: File, role: string) => {
        const form = new FormData()
        form.append('file', file)
        return $fetch(`/api/products/${productId}/images?role=${role}`, {
          method: 'POST',
          body: form
        })
      }
    },

    // ── The composition: Media × MultiLingual, joined in application code ──
    mediaText: {
      get: (mediaFileId: string) =>
        $fetch<{
          mediaFileId: string
          fileName: string
          current: MediaTextValues
          byLanguage: Record<string, Record<string, string | null>>
        }>(`/api/media-text/${mediaFileId}`, { headers: headers() }),

      set: (mediaFileId: string, values: Record<string, MediaTextValues>) =>
        $fetch(`/api/media-text/${mediaFileId}`, { method: 'POST', body: { values } })
    },

    // ── The composition: Settings × Media ─────────────────────────────────
    branding: {
      get: () => $fetch<{
        storeName: string
        accentColor: string
        logoFileId: string
        logo: { id: string, fileName: string, url: string } | null
      }>('/api/branding'),

      uploadLogo: (file: File) => {
        const form = new FormData()
        form.append('file', file)
        return $fetch('/api/branding/logo', { method: 'POST', body: form })
      }
    }
  }
}
