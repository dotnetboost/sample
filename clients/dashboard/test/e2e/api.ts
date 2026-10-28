import type { Page, Route } from '@playwright/test'
import type {
  AuditEntities, AuditPage, Category, Language, MediaPage, ProductPage, SettingGroupDescriptor,
  SettingGroupSchema
} from '../../app/types'

/**
 * The sample API, answered from the browser.
 *
 * Every `/api/**` request the page makes is fulfilled here by `page.route`, so nothing reaches the
 * Nitro proxy and nothing behind it has to be running — no .NET API, no PostgreSQL. The data is
 * just enough for each page to draw a real table rather than its empty state: three languages, two
 * categories, three products, two files, three audit entries and the three settings groups.
 *
 * A request nothing here answers is NOT quietly given an empty body. It gets a 404 and is recorded
 * in `unhandled`, and every test fails on a non-empty list — so a page that starts calling a new
 * endpoint says so here instead of rendering its error state and passing.
 */

const hoursAgo = (hours: number) => new Date(Date.now() - hours * 3_600_000).toISOString()

export const languages: Language[] = [
  { code: 'en', name: 'English', direction: 'ltr', isDefault: true, isActive: true },
  { code: 'ar', name: 'العربية', direction: 'rtl', isDefault: false, isActive: true },
  { code: 'fr', name: 'Français', direction: 'ltr', isDefault: false, isActive: true }
]

const categories: Category[] = [
  { id: 1, name: 'Phones', description: 'Handsets of every size.', slug: 'phones', products: 2, banner: null },
  { id: 2, name: 'Cases', description: 'Protection for the phones.', slug: 'cases', products: 1, banner: null }
]

const products: ProductPage = {
  items: [
    { id: 1, name: 'Pixel 9', description: 'A phone.', sku: 'PX-9', price: 799, categoryId: 1, primary: null, images: [] },
    { id: 2, name: 'Galaxy S25', description: 'Another phone.', sku: 'GS-25', price: 899, categoryId: 1, primary: null, images: [] },
    { id: 3, name: 'Leather case', description: 'For the first one.', sku: 'LC-1', price: 39, categoryId: 2, primary: null, images: [] }
  ],
  total: 3,
  skip: 0,
  take: 10
}

const media: MediaPage = {
  items: [
    {
      id: '0b9f1a52-4c1e-4f7e-9a3d-2f6c1d1e0a01', fileName: 'storefront.png', contentType: 'image/png',
      size: 48_213, checksum: null, category: 'branding', tags: ['logo'], metadata: {}, attachments: null,
      uploadedBy: 'sample-user', createdAt: hoursAgo(30), updatedAt: null, url: null
    },
    {
      id: '0b9f1a52-4c1e-4f7e-9a3d-2f6c1d1e0a02', fileName: 'price-list.pdf', contentType: 'application/pdf',
      size: 120_934, checksum: null, category: null, tags: [], metadata: {}, attachments: null,
      uploadedBy: 'sample-user', createdAt: hoursAgo(5), updatedAt: null, url: null
    }
  ],
  total: 2,
  skip: 0,
  take: 24
}

/** The smallest valid PNG, for any `<img>` pointed at a file's content route. */
const PIXEL = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=',
  'base64')

const groups: SettingGroupDescriptor[] = [
  { route: 'branding', name: 'BrandingSettings', requiresAuthorization: false },
  { route: 'catalog', name: 'CatalogSettings', requiresAuthorization: false },
  { route: 'auditing', name: 'AuditingSettings', requiresAuthorization: false }
]

const settings: Record<string, object> = {
  branding: {
    storeName: 'DotNetBoost Store',
    accentColor: 'blue',
    headerLogoSize: 32,
    images: [],
    owner: { entityName: 'Branding', entityId: 'branding' },
    logo: null,
    resolved: {}
  },
  catalog: { pageSize: 10, currencySymbol: '$' },
  auditing: { pruneEnabled: true, retentionDays: 90, pruneIntervalHours: 24 }
}

const schemas: Record<string, SettingGroupSchema> = {
  branding: {
    route: 'branding',
    name: 'BrandingSettings',
    properties: [
      { name: 'storeName', type: 'String', nullable: false, default: 'DotNetBoost Store', sensitive: false, constraints: { required: true, maxLength: 80, translated: true } },
      { name: 'accentColor', type: 'String', nullable: false, default: 'blue', sensitive: false },
      { name: 'headerLogoSize', type: 'Int32', nullable: false, default: 32, sensitive: false, constraints: { min: 16, max: 64 } }
    ]
  },
  catalog: {
    route: 'catalog',
    name: 'CatalogSettings',
    properties: [
      { name: 'pageSize', type: 'Int32', nullable: false, default: 20, sensitive: false, constraints: { min: 1, max: 100 } },
      { name: 'currencySymbol', type: 'String', nullable: false, default: '$', sensitive: false, constraints: { required: true, maxLength: 3 } }
    ]
  },
  auditing: {
    route: 'auditing',
    name: 'AuditingSettings',
    properties: [
      { name: 'pruneEnabled', type: 'Boolean', nullable: false, default: true, sensitive: false },
      { name: 'retentionDays', type: 'Int32', nullable: false, default: 90, sensitive: false, constraints: { min: 1, max: 3650 } },
      { name: 'pruneIntervalHours', type: 'Int32', nullable: false, default: 24, sensitive: false, constraints: { min: 1, max: 168 } }
    ]
  }
}

const auditEntities: AuditEntities = {
  entities: [
    { name: 'Category', clrType: 'DotNetBoostSample.Api.Catalog.Category', audited: true, optedIn: false, ignored: false, retentionDays: null, hasOwnRetention: false },
    { name: 'Product', clrType: 'DotNetBoostSample.Api.Catalog.Product', audited: true, optedIn: false, ignored: false, retentionDays: null, hasOwnRetention: false }
  ],
  maxPageSize: 200
}

/** A function rather than a constant: the times are relative to now, so the overview's chart has them in range. */
const auditPage = (): AuditPage => ({
  entries: [
    {
      id: 'a0000000-0000-0000-0000-000000000003', entityName: 'Product', entityId: '3', action: 'Updated',
      actor: 'sample-user', correlationId: 'c0000000-0000-0000-0000-000000000002', occurredAt: hoursAgo(3), metadata: null,
      changes: [{ propertyName: 'Price', oldValue: '35', newValue: '39', isRedacted: false, isTruncated: false }]
    },
    {
      id: 'a0000000-0000-0000-0000-000000000002', entityName: 'Product', entityId: '3', action: 'Created',
      actor: 'sample-user', correlationId: 'c0000000-0000-0000-0000-000000000001', occurredAt: hoursAgo(26), metadata: null,
      changes: [
        { propertyName: 'Sku', oldValue: null, newValue: 'LC-1', isRedacted: false, isTruncated: false },
        { propertyName: 'Price', oldValue: null, newValue: '35', isRedacted: false, isTruncated: false }
      ]
    },
    {
      id: 'a0000000-0000-0000-0000-000000000001', entityName: 'Category', entityId: '2', action: 'Created',
      actor: null, correlationId: null, occurredAt: hoursAgo(50), metadata: null,
      changes: [{ propertyName: 'Slug', oldValue: null, newValue: 'cases', isRedacted: false, isTruncated: false }]
    }
  ],
  total: 3,
  skip: 0,
  take: 3,
  nextCursor: null
})

type Answer = { status?: number, json?: unknown, body?: Buffer, contentType?: string }

/** `METHOD /path` → its answer. Paths are matched exactly, without the query string. */
const answers: Record<string, () => Answer> = {
  'GET /api/languages': () => ({ json: languages }),
  'GET /api/categories': () => ({ json: categories }),
  'GET /api/products': () => ({ json: products }),
  'GET /api/media': () => ({ json: media }),
  'GET /api/media/roles/Branding': () => ({ json: [{ name: 'logo', multiple: false }] }),
  'GET /api/translations/Branding/branding': () => ({ json: { translations: { ar: { StoreName: 'متجر DotNetBoost' } } } }),
  'GET /api/settings': () => ({ json: groups }),
  'GET /api/audit': () => ({ json: auditPage() }),
  'GET /api/audit/entities': () => ({ json: auditEntities }),
  ...Object.fromEntries(Object.keys(settings).flatMap(route => [
    [`GET /api/settings/${route}`, () => ({ json: settings[route] })],
    [`GET /api/settings/${route}/schema`, () => ({ json: schemas[route] })]
  ])),
  ...Object.fromEntries(media.items.map(file =>
    [`GET /api/media/${file.id}/content`, () => ({ body: PIXEL, contentType: 'image/png' })]))
}

/**
 * Installs the stubs on a page, and returns the list of requests none of them answered.
 *
 * Register a test's own `page.route` AFTER this: Playwright tries the most recently added handler
 * first, so a test can answer one request differently — a refusal, say — and leave the rest here.
 */
export async function stubApi(page: Page): Promise<string[]> {
  const unhandled: string[] = []

  // A predicate rather than `'**/api/**'`: in development the page also loads modules from paths
  // like `/_nuxt/@fs/…/devtools-api/lib/esm/api/index.js`, which a glob would swallow.
  await page.route(url => url.pathname === '/api' || url.pathname.startsWith('/api/'), async (route: Route) => {
    const request = route.request()
    const { pathname } = new URL(request.url())
    const key = `${request.method()} ${pathname}`
    const answer = answers[key]

    if (!answer) {
      unhandled.push(key)
      return route.fulfill({ status: 404, json: { title: `No stub for ${key}`, status: 404 } })
    }

    const { status = 200, json, body, contentType } = answer()

    return json === undefined
      ? route.fulfill({ status, body, contentType })
      : route.fulfill({ status, json })
  })

  return unhandled
}
