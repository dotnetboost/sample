import { mountSuspended, registerEndpoint } from '@nuxt/test-utils/runtime'
import { enableAutoUnmount } from '@vue/test-utils'
import { createError, defineEventHandler, getQuery } from 'h3'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import type { AuditEntry } from '~/types'

/**
 * How the Audit page asks the trail its question, and what happens when the question changes.
 *
 * <b>Why this file exists.</b> The three properties below were all wrong in a first cut of
 * `useAuditTrail`, and none of them is visible from inside one request:
 *
 * <ul>
 *   <li><b>the cursor outlived the filter.</b> A cursor is a position in one ordered, filtered
 *   trail. Kept across a filter change it addresses a place in a trail that is no longer being
 *   read, so the API was handed `after` from the old walk together with the new filter — and
 *   answered with entries from the middle of a set nobody asked for, or with nothing at all, which
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
 * `GET api/audit`, close enough to the real one where it matters: it filters by actor, it pages by cursor, and it refuses `skip` and `after` together the way the
 * API does — which is what makes "never send `skip`" an assertion rather than a convention.
 *
 * The cursor is the last entry's id. The real tokens are opaque, and nothing in the composable may
 * read one, which is exactly why a stand-in can stand in.
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
  const start = query.after ? matching.findIndex(entry => entry.id === query.after) + 1 : 0
  const entries = matching.slice(start, start + take)

  return {
    entries,
    // Never counted: the dashboard does not ask for a total, and `null` is what the API answers
    // when it has not been asked. A client that paginated against it would be paginating against
    // "not counted".
    total: null,
    skip: 0,
    // How many came back, which is what the real endpoint reports — not the size that was asked
    // for. Mimicked because a client that reads it as the page size reads a short page as a full
    // one, and the pager's "is there more" hangs off exactly that.
    take: entries.length,
    // Returned for every page that holds anything, the last one included. It says where this page
    // ended and never that another exists, which is the other half of the same trap.
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

  it('walks by cursor, and starts the walk again whenever the question changes', async () => {
    const { trail } = await mountTrail()

    await vi.waitFor(() => expect(trail.entries.value).toHaveLength(3))

    // One entry a page, so the walk has somewhere to go.
    await trail.setPageSize(1)

    expect(trail.entries.value.map(entry => entry.id)).toEqual(['e3'])
    expect(trail.page.value).toBe(1)
    expect(trail.hasNewer.value).toBe(false)
    expect(trail.hasOlder.value).toBe(true)

    await trail.older()

    // The next page is asked for from where the last one ended, by the token the API handed back.
    expect(reads.at(-1)).toMatchObject({ after: 'e3' })
    expect(trail.entries.value.map(entry => entry.id)).toEqual(['e2'])
    expect(trail.page.value).toBe(2)

    // Now the question changes. The cursor from the old walk must not go with it: `e3` is a
    // position in the unfiltered trail, and bob has no entry there at all — sent together with
    // his name the API answers empty, which reads as "bob changed nothing".
    await trail.setFilters({ actor: 'bob@example.com' })

    expect(reads.at(-1)!.after).toBeUndefined()
    expect(reads.at(-1)).toMatchObject({ actor: 'bob@example.com' })
    expect(trail.page.value).toBe(1)
    expect(trail.entries.value.map(entry => entry.id)).toEqual(['e3'])

    // Bob has exactly one entry and a page holds exactly one, so this is a full page that happens
    // to be the last — and `nextCursor` says where the page ended, never that another exists. A
    // cursor walk finds out by asking: the answer is an empty page with a step back from it, which
    // is the honest end of a walk that deliberately never counted the trail.
    expect(trail.hasOlder.value).toBe(true)

    await trail.older()

    expect(trail.entries.value).toEqual([])
    expect(trail.page.value).toBe(2)
    expect(trail.hasNewer.value).toBe(true)
    expect(trail.hasOlder.value).toBe(false)

    await trail.newer()

    expect(trail.entries.value.map(entry => entry.id)).toEqual(['e3'])
    expect(reads.at(-1)!.after).toBeUndefined()

    // `skip` is never sent, by anything, ever: the API refuses it together with `after`, and an
    // offset re-reads and discards the whole trail ahead of the page it serves.
    expect(reads.every(read => read.skip === undefined)).toBe(true)
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
