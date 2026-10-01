/**
 * Every call the dashboard makes, grouped by which library answers it.
 *
 * Worth reading as documentation of the composition: `media`, `multiLingual` and `settings` talk
 * to three libraries that know nothing about each other; `catalog`, `mediaText` and `branding`
 * talk to this application's own endpoints, which are where the joining happens.
 */
export interface Language {
  code: string
  name: string
  direction: number | string
  isDefault: boolean
  isActive: boolean
}

export interface MediaFile {
  id: string
  fileName: string
  contentType: string
  size: number
  altText?: string | null
  caption?: string | null
  /** Signed link; null on the FileSystem backend. Use mediaUrl(file) instead. */
  url?: string | null
}

/** languageCode → (propertyName → value) — the shape both the API and the editor use. */
export type Translations = Record<string, Record<string, string | null>>

export interface Category {
  id: number
  name: string
  description: string
  slug: string
  products: number
  banner: MediaFile | null
}

export interface CategoryDetail {
  id: number
  slug: string
  translations: Translations
  banner: MediaFile | null
}

export interface Product {
  id: number
  name: string
  description: string
  sku: string
  price: number
  categoryId: number
  primary: MediaFile | null
  images: MediaFile[]
}

export interface ProductDetail {
  id: number
  sku: string
  price: number
  categoryId: number
  translations: Translations
  primary: MediaFile | null
  gallery: MediaFile[]
}

/** The language the dashboard reads in. Sent as Accept-Language on every call. */
export const useLanguage = () => useState<string>('language', () => 'en')

export function useApi() {
  const language = useLanguage()
  const headers = () => ({ 'Accept-Language': language.value })

  return {
    // ── DotNetBoost.MultiLingual ──────────────────────────────────────────
    multiLingual: {
      languages: () => $fetch<Language[]>('/api/languages'),
      create: (body: { code: string, name: string, direction?: string }) =>
        $fetch('/api/languages', { method: 'POST', body }),
      remove: (code: string) => $fetch(`/api/languages/${code}`, { method: 'DELETE' })
    },

    // ── DotNetBoost.Media ─────────────────────────────────────────────────
    media: {
      list: () => $fetch<{ items: MediaFile[], total: number }>('/api/media?take=100'),
      remove: (id: string) => $fetch(`/api/media/${id}`, { method: 'DELETE' }),
      upload: (file: File) => {
        const form = new FormData()
        form.append('file', file)
        return $fetch<MediaFile>('/api/media', { method: 'POST', body: form })
      }
    },

    // ── DotNetBoost.Settings ──────────────────────────────────────────────
    settings: {
      get: <T>(group: string) => $fetch<T>(`/api/settings/${group}`),
      set: <T>(group: string, body: T) =>
        $fetch(`/api/settings/${group}`, { method: 'POST', body })
    },

    // ── This application: the catalogue ───────────────────────────────────
    categories: {
      list: () => $fetch<Category[]>('/api/categories', { headers: headers() }),
      get: (id: number) => $fetch<CategoryDetail>(`/api/categories/${id}`),
      create: (body: { slug: string, translations: Translations }) =>
        $fetch('/api/categories', { method: 'POST', body }),
      update: (id: number, body: { slug: string, translations: Translations }) =>
        $fetch(`/api/categories/${id}`, { method: 'PUT', body }),
      remove: (id: number) => $fetch(`/api/categories/${id}`, { method: 'DELETE' }),
      setBanner: (id: number, mediaFileId: string | null) =>
        $fetch(`/api/categories/${id}/banner`, { method: 'PUT', body: { mediaFileId } })
    },

    products: {
      list: () => $fetch<Product[]>('/api/products', { headers: headers() }),
      get: (id: number) => $fetch<ProductDetail>(`/api/products/${id}`),
      create: (body: { sku: string, price: number, categoryId: number, translations: Translations }) =>
        $fetch('/api/products', { method: 'POST', body }),
      update: (id: number, body: { sku: string, price: number, categoryId: number, translations: Translations }) =>
        $fetch(`/api/products/${id}`, { method: 'PUT', body }),
      remove: (id: number) => $fetch(`/api/products/${id}`, { method: 'DELETE' }),
      setPrimary: (id: number, mediaFileId: string | null) =>
        $fetch(`/api/products/${id}/primary`, { method: 'PUT', body: { mediaFileId } }),
      addToGallery: (id: number, mediaFileId: string) =>
        $fetch(`/api/products/${id}/gallery`, { method: 'POST', body: { mediaFileId } }),
      removeFromGallery: (id: number, mediaFileId: string) =>
        $fetch(`/api/products/${id}/gallery/${mediaFileId}`, { method: 'DELETE' })
    },

    // ── The composition: Media × MultiLingual ─────────────────────────────
    mediaText: {
      get: (mediaFileId: string) =>
        $fetch<{
          mediaFileId: string
          fileName: string
          current: { altText: string | null, caption: string | null }
          byLanguage: Record<string, Record<string, string | null>>
        }>(`/api/media-text/${mediaFileId}`, { headers: headers() }),

      set: (mediaFileId: string, values: Record<string, { altText: string | null, caption: string | null }>) =>
        $fetch(`/api/media-text/${mediaFileId}`, { method: 'POST', body: { values } }),

      /** Alt text for many files in one call, in the current language. */
      resolve: (ids: string[]) =>
        $fetch<Record<string, { altText: string | null, caption: string | null }>>(
          '/api/media-text/resolve', { method: 'POST', body: { ids }, headers: headers() })
    },

    // ── The composition: Settings × Media ─────────────────────────────────
    branding: {
      get: () => $fetch<{
        storeName: string
        accentColor: string
        logoFileId: string
        logo: MediaFile | null
      }>('/api/branding'),

      uploadLogo: (file: File) => {
        const form = new FormData()
        form.append('file', file)
        return $fetch('/api/branding/logo', { method: 'POST', body: form })
      }
    }
  }
}
