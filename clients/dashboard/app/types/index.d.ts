/** One stored file, as `GET api/media` and friends return it. */
export interface MediaFile {
  id: string
  fileName: string
  contentType: string
  size: number
  checksum: string | null
  category: string | null
  tags: string[]
  metadata: Record<string, string>
  /**
   * Everything using this file, on `GET api/media/{id}` only. `null` in a listing, which does not
   * carry them — a page of files would need a query each to fill. Read the one file to see them.
   */
  attachments: MediaAttachment[] | null
  uploadedBy: string | null
  createdAt: string
  updatedAt: string | null
  /**
   * A direct link to the bytes, when the storage backend mints one. `null` for a backend that
   * does not — local disk, typically — in which case the content route is the way to the file.
   */
  url: string | null
}

/**
 * One entity's use of a file. A file may have many — the same image can be one product's main
 * photo and another's third gallery shot, which is why the role lives here and not on the file.
 */
export interface MediaAttachment {
  id: string
  ownerEntityName: string
  ownerEntityId: string
  /** What the entity uses the file for: `primary`, `gallery`, `seo`, or whatever the app means. */
  role: string
  /** Its position within that role. */
  sortOrder: number
  createdAt: string
}

/** One page of `GET api/media`. */
export interface MediaPage {
  items: MediaFile[]
  total: number
  skip: number
  take: number
}

/**
 * What a listing can be ordered by. Mirrors the API's `MediaSortBy`.
 *
 * `sortOrder` is the order an entity put its own files in, so it needs an owner filter; the API
 * refuses it without one rather than quietly ignoring it.
 */
export type MediaSortBy = 'createdAt' | 'fileName' | 'size' | 'sortOrder'

/** The filter, sort and page the browser is currently showing. */
export interface MediaQuery {
  search: string
  category: string
  tag: string
  contentType: string
  sortBy: MediaSortBy
  descending: boolean
  page: number
  pageSize: number
  /** Restricts the listing to one entity's files. Set by the picker on the products page. */
  ownerEntityName?: string
  ownerEntityId?: string
  /** Narrows that entity's files to one role. Needs an owner. */
  role?: string
  /** Only files nothing is using — for finding uploads nobody ever claimed. */
  unattached?: boolean
}

/** Body of `PATCH api/media/{id}`. Omitted properties are left alone. */
export interface MediaUpdate {
  fileName?: string
  category?: string
  tags?: string[]
  metadata?: Record<string, string>
}

/**
 * One role an entity declares, as `GET api/media/roles/{entityName}` returns it.
 *
 * `multiple: false` holds one file, so attaching there sends `replace: true`; `multiple: true`
 * holds many, in order, and appends. An entity that declares nothing comes back as an empty list
 * and accepts any role.
 */
export interface MediaRole {
  name: string
  multiple: boolean
}

/**
 * Body of `POST api/media/{id}/attachments`.
 *
 * `replace` is what makes a single-valued role single-valued: true takes the role over from
 * whatever held it, false appends as a gallery does. Nothing in the schema knows that `primary`
 * holds one file and `gallery` holds many, so the caller says which it means.
 */
export interface AttachRequest {
  ownerEntityName: string
  ownerEntityId: string
  role?: string
  replace?: boolean
}

/**
 * One of an entity's files, as this application's own detail endpoints return it.
 *
 * The role and sort order come from `ListWithRolesAsync`, and `altText`/`caption` from
 * DotNetBoost.MultiLingual — two libraries' data in one object, joined in application code.
 */
export interface EntityImage {
  id: string
  fileName: string
  contentType: string
  size: number
  /** What this entity uses it for — `banner`, `primary`, `gallery`. */
  role: string
  /** Its position within that role. */
  sortOrder: number
  /** Resolved in the request's language, with fallback to the default. */
  altText: string | null
  caption: string | null
  url: string
}

/** A language, as `GET api/languages` returns it. */
export interface Language {
  code: string
  name: string
  direction: number | string
  isDefault: boolean
  isActive: boolean
}

/** languageCode -> (propertyName -> value): what the API returns and the editor hands back. */
export type Translations = Record<string, Record<string, string | null>>

/** A category in the list, translated into the request's language. */
export interface Category {
  id: number
  name: string
  description: string
  slug: string
  products: number
  banner: EntityImage | null
}

/** A category for editing: every language at once, and every attached file with its role. */
export interface CategoryDetail {
  id: number
  slug: string
  translations: Translations
  images: EntityImage[]
}

/** A product in the list, translated into the request's language. */
export interface Product {
  id: number
  name: string
  description: string
  sku: string
  price: number
  categoryId: number
  primary: EntityImage | null
  images: EntityImage[]
}

/** A product for editing. */
export interface ProductDetail {
  id: number
  sku: string
  price: number
  categoryId: number
  translations: Translations
  images: EntityImage[]
}

/** Alt text and caption for one file in one language. */
export interface MediaTextValues {
  altText: string | null
  caption: string | null
}

/** RFC 7807, as the API's problem-details mapping writes it. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  /** `ValidationProblemDetails` adds this. */
  errors?: Record<string, string[]>
  /** The API attaches these to some problems — `limit`, `size`, `detectedContentType`, … */
  [key: string]: unknown
}
