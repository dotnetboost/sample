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
 * A file as a listing or a thumbnail carries it: the file, and its text.
 *
 * `altText` and `caption` are DotNetBoost.MultiLingual rows, not media data — two libraries' data
 * in one object, joined in application code.
 *
 * **No role.** A role belongs to an attachment rather than to a file, and the media library lets
 * one file be attached several times over, so there is no single role for a listing to report.
 * {@link EntityImage} is the shape that has one, and only the detail endpoints return it.
 *
 * **No url either.** `contentUrl(id)` builds it — one rule, in one place. The API used to send the
 * same string as well and nothing read it, because a signed backend URL expires and is null on
 * local disk, so every image on screen is rendered from the id regardless.
 */
export interface MediaRef {
  id: string
  fileName: string
  contentType: string
  size: number
  /** Resolved in the request's language, with fallback to the default. */
  altText: string | null
  caption: string | null
}

/**
 * One of an entity's files **in one of its roles**, as this application's own detail endpoints
 * return it — what the role boxes on an editor are drawn from.
 *
 * The role and sort order come from `ListWithRolesAsync`, which answers per link rather than per
 * file: a product using one file as both its `primary` and its `gallery` image appears here twice,
 * once under each role, which is exactly what a role box needs and exactly what a count does not.
 */
export interface EntityImage extends MediaRef {
  /** What this entity uses it for — `banner`, `primary`, `gallery`. */
  role: string
  /** Its position within that role. */
  sortOrder: number
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
  banner: MediaRef | null
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
  primary: MediaRef | null
  /** Distinct files: a file in two roles is one image here. */
  images: MediaRef[]
}

/**
 * One page of `GET api/products`.
 *
 * The same shape as {@link MediaPage}, because the API answers a catalogue listing the way
 * DotNetBoost.Media answers its own. `take` is what the page actually holds — the
 * `CatalogSettings.PageSize` setting when the caller asked for nothing — so a pager reads it from
 * the response rather than needing to know the setting.
 */
export interface ProductPage {
  items: Product[]
  total: number
  skip: number
  take: number
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
/**
 * An owner as DotNetBoost.Media addresses one: two strings, and no row behind them.
 *
 * Published by a settings group's projector — `owner` on `GET /api/settings/{route}` — because
 * nothing else states it. The group list answers with the CLR class name, and `[MediaEntity]`
 * pins the stored name away from that on purpose, so a client cannot derive it. The roles endpoint
 * answers `200 []` for a name nobody declared rather than refusing it, which is why guessing fails
 * silently and the address has to travel.
 */
export interface MediaOwner {
  entityName: string
  entityId: string
}

export type SettingValues = Record<string, SettingValue>

/**
 * Presentation metadata for one property. Everything here is optional — the form is generated from
 * the values the API returns, so a property added to the C# class renders without an entry.
 */
/**
 * One property, as `GET /api/settings/{route}/schema` describes it.
 *
 * The server's half of a generated form: the property list, its CLR types, its compile-time
 * defaults, which properties are `[Sensitive]`, and the constraints its validators enforce. Before
 * this endpoint the client guessed at all of it — `headerLogoSize: { min: 16, max: 64 }` lived in
 * `utils/settings.ts` and matched nothing in C#.
 */
export interface SettingPropertySchema {
  /** camelCase, exactly as the group's own GET returns it. */
  name: string
  /** CLR type name — `Int32`, `String`, `Boolean`. */
  type: string
  nullable: boolean
  /** `[SettingDefault]`, or the value a fresh instance initialises it to. Withheld when sensitive. */
  default?: unknown
  /** Mirrors `[Sensitive]` on the C# property. */
  sensitive: boolean
  /**
   * What the registered validators enforce: `min`, `max`, `minLength`, `maxLength`, `pattern`,
   * `required`, `exclusiveMin`, `exclusiveMax`. Absent when no `ISettingSchemaContributor` can
   * describe the group.
   */
  constraints?: Record<string, unknown>
}

/** `GET /api/settings/{route}/schema`. */
export interface SettingGroupSchema {
  route: string
  name: string
  properties: SettingPropertySchema[]
}

/** One row of `GET /api/settings` — the registered groups, values excluded. */
export interface SettingGroupDescriptor {
  route: string
  name: string
  requiresAuthorization: boolean
}

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
  /** Whether the group carries `[Authorize]`, as `GET /api/settings` reports it. */
  requiresAuthorization: boolean
  /**
   * From `utils/settings.ts`, with a default for a group this dashboard has never heard of — the
   * one thing about a settings group that no API can answer. Everything else, down to which
   * properties exist and what bounds they carry, comes from the group's own schema.
   */
  icon: string
}
