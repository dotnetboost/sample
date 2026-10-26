import { mountSuspended, registerEndpoint } from '@nuxt/test-utils/runtime'
import { enableAutoUnmount } from '@vue/test-utils'
import { createError, defineEventHandler, getQuery } from 'h3'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import type { AuditEntry } from '~/types'

/**
 * How the Audit page asks the trail its question, and what happens when the question changes.
 *
 * <b>Why this file exists.</b> None of the three properties below is visible from inside one
 * request:
 *
 * <ul>
 *   <li><b>the page number must not outlive the filter.</b> Page 2 of the whole trail can be past
 *   the end of a filter that matches one page, and the API answers that with nothing at all, which
 *   reads as "no entries match" for a filter that matches plenty;</li>
 *
 *   <li><b>the second question was answered with the first one's page.</b> The entry is registered
 *   with `dedupe: 'defer'`, which is right for the readers that arrive in the same tick and wrong
 *   for every deliberate move: typing an actor while the first page was still in flight handed back
 *   the pending promise, so the box said `bob@example.com` over the unfiltered trail. Every move
 *   refreshes with `dedupe: 'cancel'` for that reason;</li>
 *
 *   <li><b>the declared entities were read twice</b> — once for the filter bar's entity list and
 *   once for the page bound — which is the bug `useLanguageList.spec.ts` documents at length,
 *   arriving a second time on a second endpoint.</li>
 * </ul>
 */

/** The trail the fake endpoint serves, newest first — the order the API answers in. */
const TRAIL: AuditEntry[] = [
  {
    id: 'e3',
    entityName: 'Product',
    entityId: '7',
    action: 'Updated',
    actor: 'bob@example.com',
    correlationId: 'c3',
    occurredAt: '2026-10-24T09:00:00Z',
    metadata: null,
    changes: [{
      propertyName: 'Price', oldValue: '1.00', newValue: '42.50',
      isRedacted: false, isTruncated: false
    }]
  },
  {
    id: 'e2',
    entityName: 'Product',
    entityId: '7',
    action: 'Created',
    actor: 'alice@example.com',
    correlationId: 'c2',
    occurredAt: '2026-10-23T09:00:00Z',
    metadata: null,
    changes: []
  },
  {
    id: 'e1',
    entityName: 'Category',
    entityId: '2',
    action: 'Created',
    actor: null,
    correlationId: 'c1',
    occurredAt: '2026-10-22T09:00:00Z',
    metadata: null,
    changes: []
  }
]

/** Every query the trail endpoint was asked, in order. Most of what is under test. */
let reads: Record<string, string | undefined>[]
/** How many times the declared entities were asked for, which is the other half. */
let entityReads: number
/** Held closed by the one test that asks a second question while the first is still in flight. */
let gate: Promise<void> | null

/**
 * `GET api/audit`, close enough to the real one where it matters: it filters by actor, it pages by
 * offset, it counts only when asked, and it refuses `skip` and `after` together the way the API
 * does — which is what makes "never send `after`" an assertion rather than a convention.
 */
registerEndpoint('/api/audit', defineEventHandler(async (event) => {
  const query = getQuery(event) as Record<string, string | undefined>
  reads.push(query)

  if (query.skip !== undefined && query.after !== undefined) {
    throw createError({ statusCode: 400, statusMessage: 'skip and after are mutually exclusive' })
  }

  if (gate) {
    await gate
  }

  const take = Number(query.take ?? 25)
  const matching = TRAIL.filter(entry => !query.actor || entry.actor === query.actor)
  const skip = Number(query.skip ?? 0)
  const entries = matching.slice(skip, skip + take)

  return {
    entries,
    // `null` unless asked for, which is what the API answers: "not counted", never zero.
    total: query.includeTotal === 'true' ? matching.length : null,
    skip,
    // How many came back, which is what the real endpoint reports — not the size that was asked
    // for.
    take: entries.length,
    nextCursor: entries.length ? entries[entries.length - 1]!.id : null
  }
}))

registerEndpoint('/api/audit/entities', defineEventHandler(() => {
  entityReads++

  return {
    entities: [
      { name: 'Category', clrType: 'Category', audited: true, optedIn: false, ignored: false, retentionDays: null, hasOwnRetention: false },
      { name: 'Product', clrType: 'Product', audited: true, optedIn: false, ignored: false, retentionDays: null, hasOwnRetention: false }
    ],
    maxPageSize: 50
  }
}))

/** Mounts something that reads the trail. */
async function mountTrail() {
  let trail!: ReturnType<typeof useAuditTrail>

  await mountSuspended(defineComponent({
    setup() {
      trail = useAuditTrail()

      return () => h('div')
    }
  }))

  return { trail }
}

/** A second reader of the declared entities, the way the filter bar is one. */
async function mountEntities() {
  let entities!: ReturnType<typeof useAuditEntities>

  await mountSuspended(defineComponent({
    setup() {
      entities = useAuditEntities()

      return () => h('div')
    }
  }))

  return entities
}

/**
 * The two halves of starting each test from nothing, and both are needed.
 *
 * A mounted reader outlives its test — the file is one Nuxt app — so without the unmount, the
 * previous test's component is still a reader of the same `useAsyncData` key: its own `query`, with
 * its own page size, answers over the top of the one under test and the failure reads as a
 * composable that ignored a filter. And a resolved entry is never re-run, so without the clear a
 * fresh reader finds the previous answer and asks nothing at all.
 */
enableAutoUnmount(afterEach)

async function clearTrailData() {
  await mountSuspended(defineComponent({
    setup() {
      clearNuxtData([AUDIT_TRAIL_KEY, AUDIT_ENTITIES_KEY, AUDIT_ENTITY_KEY])

      return () => h('div')
    }
  }))
}

beforeEach(async () => {
  reads = []
  entityReads = 0
  gate = null

  await clearTrailData()
})

describe('useAuditTrail', () => {
  it('reads the declared entities once for every reader, and takes the page bound from them', async () => {
    const { trail } = await mountTrail()

    await vi.waitFor(() => expect(trail.entries.value).toHaveLength(3))

    // The filter bar's entity list and the page-size control are two readers of one endpoint. One
    // `useAsyncData` key, so the second finds the entry resolved and reads it.
    const alsoReading = await mountEntities()

    expect(entityReads).toBe(1)
    expect(trail.entities.value.map(entity => entity.name)).toEqual(['Category', 'Product'])
    expect(alsoReading.entities.value).toHaveLength(2)

    // Read rather than hard-coded: a client that assumes a cap has a number to keep in step with
    // the server's by hand, and one that asks for more is clamped rather than refused.
    expect(trail.maxPageSize.value).toBe(50)
  })

  it('pages by offset with a count, and goes back to page 1 whenever the question changes', async () => {
    const { trail } = await mountTrail()

    await vi.waitFor(() => expect(trail.entries.value).toHaveLength(3))

    // The count comes with every page: it is what the numbered pager is drawn from.
    expect(reads.at(-1)).toMatchObject({ skip: '0', includeTotal: 'true' })
    expect(trail.total.value).toBe(3)

    // One entry a page, so there are pages to move between.
    await trail.setPageSize(1)

    expect(trail.entries.value.map(entry => entry.id)).toEqual(['e3'])
    expect(trail.page.value).toBe(1)

    // Straight to the last page, which only an offset can do.
    await trail.goTo(3)

    expect(reads.at(-1)).toMatchObject({ skip: '2', take: '1' })
    expect(trail.entries.value.map(entry => entry.id)).toEqual(['e1'])
    expect(trail.page.value).toBe(3)

    // Now the question changes. Bob has one entry, so page 3 of his trail is empty — kept across
    // the change, it would read as "bob changed nothing".
    await trail.setFilters({ actor: 'bob@example.com' })

    expect(reads.at(-1)).toMatchObject({ actor: 'bob@example.com', skip: '0' })
    expect(trail.page.value).toBe(1)
    expect(trail.total.value).toBe(1)
    expect(trail.entries.value.map(entry => entry.id)).toEqual(['e3'])

    // `after` is never sent, by anything: the API refuses it together with `skip`.
    expect(reads.every(read => read.after === undefined)).toBe(true)
  })

  it('does not answer a new question with the answer already in flight', async () => {
    let open!: () => void
    gate = new Promise<void>((resolve) => {
      open = resolve
    })

    // The first page is still out: nothing has resolved, and the entry is registered with
    // `dedupe: 'defer'`.
    const { trail } = await mountTrail()

    await vi.waitFor(() => expect(reads).toHaveLength(1))

    const filtered = trail.setFilters({ actor: 'bob@example.com' })

    // Under `defer` this stays at one request and the page in flight — the unfiltered trail — is
    // handed back as the answer to a question about bob. `dedupe: 'cancel'` is why there are two.
    await vi.waitFor(() => expect(reads).toHaveLength(2))

    open()
    await filtered

    expect(trail.entries.value.map(entry => entry.actor)).toEqual(['bob@example.com'])
  })
})
