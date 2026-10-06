import type {
  Category, CategoryDetail, CreateResult, Language, MediaTextValues, ProductDetail, ProductPage,
  Translations, WriteResult
} from '~/types'

/**
 * This application's own endpoints, plus the two library endpoints the catalogue pages need.
 *
 * The media library is NOT wrapped here: `useMediaLibrary` talks to `/api/media` directly, exactly
 * as the DotNetBoost.Media dashboard does, so searching, filtering, sorting, paging, uploading,
 * attaching and detaching are that library's own behaviour rather than a second implementation.
 */

/**
 * The translation routes and the property names behind them.
 *
 * `MediaFile` and `Branding` are the two subject names the API exposes over HTTP — see
 * `.ExposeOverHttp(...)` in Program.cs; an unlisted name is not routed at all. The property names
 * are the C# ones (`MediaText.AltText`, `MediaText.Caption`, `BrandingSettings.StoreName`), because
 * a translation row's `PropertyName` is data rather than a bound field and is matched exactly.
 *
 * Written here once, so the one place this client depends on those names is visible rather than
 * spread through the calls below.
 */
const TRANSLATIONS = '/api/translations/MediaFile'
const BRANDING_TEXT = '/api/translations/Branding/branding'
const ALT_TEXT = 'AltText'
const CAPTION = 'Caption'
const STORE_NAME = 'StoreName'

/**
 * The language the dashboard reads in. Sent as Accept-Language on every call.
 *
 * Seeded with the default and then overwritten by `plugins/reading-language.ts` with whatever was
 * chosen last time, before the app mounts. The state itself stays plain: one shared ref, eight
 * readers, and exactly one place that knows it is remembered at all.
 */
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
      // Accept-Language on the settings endpoints too: the library ignores it, but the global
      // UseMultiLingual() middleware resolves it and the API's cache keys each group by it.
      get: <T>(group: string) => $fetch<T>(`/api/settings/${group}`, { headers: headers() }),
      /** The group's model, posted whole — the settings library validates it server side. */
      set: <T extends object>(group: string, body: T) =>
        $fetch(`/api/settings/${group}`, {
          method: 'POST',
          body: body as Record<string, unknown>,
          headers: headers()
        })
    },

    // ── This application: the catalogue ───────────────────────────────────
    //
    // Accept-Language on the writes as well as the reads. A write's own answer is now a sentence —
    // `WriteResult.message` — and the API writes it in whatever language the request was read in,
    // so a call that sends no header gets the right words in the wrong language. The `get`s stay
    // without it on purpose: they return every language at once for the editor, and a header would
    // not change what comes back.
    categories: {
      list: () => $fetch<Category[]>('/api/categories', { headers: headers() }),
      get: (id: number) => $fetch<CategoryDetail>(`/api/categories/${id}`),
      create: (body: { slug: string, translations: Translations }) =>
        $fetch<CreateResult>('/api/categories', { method: 'POST', body, headers: headers() }),
      update: (id: number, body: { slug: string, translations: Translations }) =>
        $fetch<WriteResult>(`/api/categories/${id}`, { method: 'PUT', body, headers: headers() }),
      remove: (id: number) =>
        $fetch<WriteResult>(`/api/categories/${id}`, { method: 'DELETE', headers: headers() })
    },

    products: {
      /**
       * One page, newest page size first.
       *
       * Both parameters are omitted by the catalogue list on purpose: with no `pageSize` the API
       * uses `CatalogSettings.PageSize`, which is the only way that setting reaches a screen.
       * The overview passes one because it counts across pages rather than showing them.
       */
      list: (query: { page?: number, pageSize?: number } = {}) =>
        $fetch<ProductPage>('/api/products', { query, headers: headers() }),
      get: (id: number) => $fetch<ProductDetail>(`/api/products/${id}`),
      create: (body: { sku: string, price: number, categoryId: number, translations: Translations }) =>
        $fetch<CreateResult>('/api/products', { method: 'POST', body, headers: headers() }),
      update: (id: number, body: { sku: string, price: number, categoryId: number, translations: Translations }) =>
        $fetch<WriteResult>(`/api/products/${id}`, { method: 'PUT', body, headers: headers() }),
      remove: (id: number) =>
        $fetch<WriteResult>(`/api/products/${id}`, { method: 'DELETE', headers: headers() })
    },

    // ── The composition: Media × MultiLingual ─────────────────────────────
    //
    // DotNetBoost.MultiLingual's own translation endpoints, addressing a media file as the subject
    // ("MediaFile", <the file's GUID>) the API files these rows under. The sample used to hand-write
    // this CRUD as `/api/media-text`; the library ships it now, exposed per entity name in
    // Program.cs, so what is left here is the two property names and the shape this editor wants.
    mediaText: {
      /** Every stored language, as `{ altText, caption }` per language code. */
      get: async (mediaFileId: string) => {
        const { translations } = await $fetch<{ translations: Translations }>(
          `${TRANSLATIONS}/${mediaFileId}`, { headers: headers() })

        return Object.fromEntries(
          Object.entries(translations ?? {}).map(([code, values]) => [code, {
            altText: values[ALT_TEXT] ?? null,
            caption: values[CAPTION] ?? null
          } satisfies MediaTextValues])) as Record<string, MediaTextValues>
      },

      /**
       * Writes every language supplied, as one batch. A null removes that row rather than storing
       * a null. Answers 204, so there is nothing to read back.
       */
      set: (mediaFileId: string, values: Record<string, MediaTextValues>) =>
        $fetch(`${TRANSLATIONS}/${mediaFileId}`, {
          method: 'PUT',
          body: {
            translations: Object.fromEntries(
              Object.entries(values).map(([code, v]) => [code, {
                [ALT_TEXT]: v.altText?.trim() ? v.altText : null,
                [CAPTION]: v.caption?.trim() ? v.caption : null
              }]))
          }
        })

      // No `remove`. Deleting a file takes its alt text with it: the API registers
      // MediaTextCleanup as an IMediaFilesDeletedHandler, so the cascade happens for every delete
      // whatever triggered it — including DotNetBoost.Media's own DELETE /api/media/{id}, which is
      // the one this dashboard calls. Following up by hand is what that handler replaced.
    },

    // ── The composition: Settings × Media ─────────────────────────────────
    branding: {
      // `images` is the same shape the catalogue returns, so branding's media is drawn by the very
      // same component — the logo is an attachment like any other, on an owner with no table.
      // Prefer useBranding(), which shares one fetch between the header and the settings page.
      get: () => $fetch<Branding>('/api/branding', { headers: headers() }),

      /**
       * Every stored language for the branding group's translated text, through MultiLingual's own
       * translation endpoint — the group is the subject ("Branding", "branding") that its
       * [MultiLingualEntity] and Id declare. `/api/branding/text` used to hand-write this.
       */
      text: async () => {
        const { translations } = await $fetch<{ translations: Translations }>(BRANDING_TEXT)
        return translations ?? {}
      },

      /** Writes one or more languages. The default language is NOT written here — it is a
          setting, and lives in the settings row that the group form edits. Answers 204. */
      setText: (values: Record<string, string | null>) =>
        $fetch(BRANDING_TEXT, {
          method: 'PUT',
          body: {
            translations: Object.fromEntries(
              Object.entries(values).map(([code, value]) => [code, { [STORE_NAME]: value }]))
          }
        })
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
