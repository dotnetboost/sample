import type {
  Category, CategoryDetail, EntityImage, Language, MediaTextValues, Product, ProductDetail,
  Translations
} from '~/types'

/**
 * This application's own endpoints, plus the two library endpoints the catalogue pages need.
 *
 * The media library is NOT wrapped here: `useMediaLibrary` talks to `/api/media` directly, exactly
 * as the DotNetBoost.Media dashboard does, so searching, filtering, sorting, paging, uploading,
 * attaching and detaching are that library's own behaviour rather than a second implementation.
 */

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
        $fetch('/api/languages', { method: 'POST', body })
    },

    // ── DotNetBoost.Settings ──────────────────────────────────────────────
    settings: {
      get: <T>(group: string) => $fetch<T>(`/api/settings/${group}`),
      /** The group's model, posted whole — the settings library validates it server side. */
      set: <T extends object>(group: string, body: T) =>
        $fetch(`/api/settings/${group}`, { method: 'POST', body: body as Record<string, unknown> })
    },

    // ── This application: the catalogue ───────────────────────────────────
    categories: {
      list: () => $fetch<Category[]>('/api/categories', { headers: headers() }),
      get: (id: number) => $fetch<CategoryDetail>(`/api/categories/${id}`),
      create: (body: { slug: string, translations: Translations }) =>
        $fetch<{ id: number }>('/api/categories', { method: 'POST', body }),
      update: (id: number, body: { slug: string, translations: Translations }) =>
        $fetch(`/api/categories/${id}`, { method: 'PUT', body }),
      remove: (id: number) => $fetch(`/api/categories/${id}`, { method: 'DELETE' })
    },

    products: {
      list: () => $fetch<Product[]>('/api/products', { headers: headers() }),
      get: (id: number) => $fetch<ProductDetail>(`/api/products/${id}`),
      create: (body: { sku: string, price: number, categoryId: number, translations: Translations }) =>
        $fetch<{ id: number }>('/api/products', { method: 'POST', body }),
      update: (id: number, body: { sku: string, price: number, categoryId: number, translations: Translations }) =>
        $fetch(`/api/products/${id}`, { method: 'PUT', body }),
      remove: (id: number) => $fetch(`/api/products/${id}`, { method: 'DELETE' })
    },

    // ── The composition: Media × MultiLingual ─────────────────────────────
    mediaText: {
      get: (mediaFileId: string) =>
        $fetch<{
          mediaFileId: string
          fileName: string
          current: MediaTextValues
          byLanguage: Record<string, Record<string, string | null>>
        }>(`/api/media-text/${mediaFileId}`, { headers: headers() }),

      set: (mediaFileId: string, values: Record<string, MediaTextValues>) =>
        $fetch(`/api/media-text/${mediaFileId}`, { method: 'POST', body: { values } }),

      /** Alt text for many files in one call, in the current language. */
      resolve: (ids: string[]) =>
        $fetch<Record<string, MediaTextValues>>(
          '/api/media-text/resolve', { method: 'POST', body: { ids }, headers: headers() })
    },

    // ── The composition: Settings × Media ─────────────────────────────────
    branding: {
      // The logo comes back shaped like an attached file, so the dashboard can draw it with the
      // same box as a category banner — though nothing is attached: the setting holds the id.
      get: () => $fetch<{
        storeName: string
        accentColor: string
        logoFileId: string
        logo: EntityImage | null
      }>('/api/branding')
    }
  }
}

/**
 * Attaching and detaching, through DotNetBoost.Media's OWN endpoints.
 *
 * Deliberately not routed through this application: the role lives on the attachment, the library
 * owns attachments, and it already exposes exactly these operations. Every entity in the
 * application gets attachment handling without a single endpoint of its own.
 */
export function useAttachments() {
  const attach = (
    mediaFileId: string,
    owner: { entityName: string, entityId: string },
    role: string,
    replace: boolean
  ) => $fetch(`/api/media/${mediaFileId}/attachments`, {
    method: 'POST',
    body: { ownerEntityName: owner.entityName, ownerEntityId: owner.entityId, role, replace }
  })

  /** Needs the ATTACHMENT's id, not the file's — `GET /api/media/{id}` carries them. */
  const detach = (mediaFileId: string, attachmentId: string) =>
    $fetch(`/api/media/${mediaFileId}/attachments/${attachmentId}`, { method: 'DELETE' })

  /** Every id in the role: a partial order has no meaning, and the API refuses one. */
  const reorder = (
    owner: { entityName: string, entityId: string },
    role: string,
    mediaFileIds: string[]
  ) => $fetch('/api/media/attachments/order', {
    method: 'PUT',
    body: { ownerEntityName: owner.entityName, ownerEntityId: owner.entityId, role, mediaFileIds }
  })

  return { attach, detach, reorder }
}
