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

/** Which way a language reads. The API serialises it lowercase. */
export type TextDirection = 'ltr' | 'rtl'

/** A language, as `GET api/languages` returns it. */
export interface Language {
  code: string
  name: string
  direction: TextDirection
  /** The language content falls back to when a translation is missing. Exactly one. */
  isDefault: boolean
  /** Whether content is served in it at all. */
  isActive: boolean
}

/** Body of `POST api/languages`. */
export interface NewLanguage {
  code: string
  name: string
  /** Omit to take the direction from the runtime's culture data for the code. */
  direction?: TextDirection
  isActive?: boolean
  /** The first language created is always the default, whatever this says. */
  isDefault?: boolean
}

/** Body of `PUT api/languages/{code}`. A full replacement; the code cannot change. */
export interface LanguageUpdate {
  name: string
  direction?: TextDirection
  isActive: boolean
}

/** Result of `DELETE api/languages/{code}/translations`. */
export interface PurgeResult {
  code: string
  deletedTranslations: number
}

/** languageCode -> (propertyName -> value): what the API returns and the editor hands back. */
export type Translations = Record<string, Record<string, string | null>>

/**
 * What a catalogue write answers with: a sentence saying what it did, in the language the call
 * asked for.
 *
 * The dashboard does not compose this text — "Category phone-cases was saved." names the row and
 * the API is the only side that knows which row it ended up being. So the toast shows the API's
 * sentence under the dashboard's own short title, exactly the way a refused write already shows
 * `problemMessage(error)` under one.
 *
 * That is also why `update` and `remove` are typed at all: they used to answer 204 and there was
 * nothing to read.
 */
export interface WriteResult {
  message: string
}

/** A create, which answers with the new row's id beside the message. */
export interface CreateResult extends WriteResult {
  id: number
}

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

/** A single setting value as it travels over the REST API. */
export type SettingValue = string | number | boolean

/** One settings group as returned by `GET api/settings/{route}`. */
export type SettingValues = Record<string, SettingValue>

/**
 * Presentation metadata for one property. Everything here is optional — the form is generated from
 * the values the API returns, so a property added to the C# class renders without an entry.
 */
export interface SettingField {
  /** Renders a masked input with a reveal toggle. Mirrors `[Sensitive]` on the C# property. */
  sensitive?: boolean
  /**
   * Mirrors `[MultiLingual]` on the C# property: the value is translated, and the settings row
   * holds the DEFAULT language's version of it.
   *
   * The generated form still edits that row — it is a setting like any other — but the field is
   * badged so it is unmistakable that the box edits the default language, not whatever language
   * you happen to be reading in. Other languages are edited separately, against the translations
   * store, so there is never more than one writer for a value.
   */
  translated?: boolean
  /** Input type for string properties. */
  type?: 'text' | 'url' | 'email'
  min?: number
  max?: number
  step?: number
}

/**
 * A `[SettingGroup]`-decorated C# class, as surfaced by the dashboard.
 *
 * No label or description: a group's visible text is addressed in the message bundles by its
 * `route`, so there is nothing to keep in step between here and three locale files. See
 * `groupLabel` and `fieldLabel` in `utils/settings.ts`.
 */
export interface SettingGroup {
  /** Route segment from `[SettingGroup("…")]` — the API path this group lives on. */
  route: string
  /** C# class name. Not translated: it is the identity the API and the C# source share. */
  name: string
  icon: string
  /** Dashboard route for this group's tab. */
  to: string
  fields: Record<string, SettingField>
}
