import type { AuditEntities, AuditEntitySchema, AuditPage, AuditQuery } from '~/types'

/**
 * The key both readers of the declared entities share, so the dashboard asks for them once.
 */
export const AUDIT_ENTITIES_KEY = 'audit-entities'

/** The key the trail listing is read under. One page of one trail at a time, by design. */
export const AUDIT_TRAIL_KEY = 'audit-trail'

/** The key the selected entity's rules are read under. See {@link useAuditTrail}. */
export const AUDIT_ENTITY_KEY = 'audit-entity'

/**
 * What the trail can be filtered by, from `GET api/audit/entities`, plus the page bound every read
 * is subject to.
 *
 * One `useAsyncData` key, shared: the filter bar builds its entity list from this and the rules
 * card checks `audited` against it, and neither should cost a request of its own. The same
 * arrangement as `useLanguageList`, for the same reason — Nuxt de-duplicates by key alone and will
 * not re-run an entry that has already resolved, so two keys over one endpoint are two answers
 * that can drift apart.
 *
 * `dedupe: 'defer'` likewise: the readers register in the same tick, and under the default
 * `cancel` each arrival would abort the request in flight and issue its own.
 */
export function useAuditEntities() {
  const api = useApi()

  const asyncData = useAsyncData<AuditEntities>(
    AUDIT_ENTITIES_KEY,
    (_nuxtApp, { signal }) => api.audit.entities({ signal }),
    { dedupe: 'defer' })

  const { data, error, refresh } = asyncData

  /** Never null — the page reads straight off it before the first answer lands. */
  const entities = computed(() => data.value?.entities ?? [])

  /**
   * The largest page the API will serve, read rather than assumed.
   *
   * `0` until it has answered, which is how the page-size control knows not to offer a size yet.
   * Hard-coding a cap here would be a number to keep in step with the server's by hand, and a
   * client that asks for more than the cap is not refused — it is clamped, and only `take` in the
   * response says so.
   */
  const maxPageSize = computed(() => data.value?.maxPageSize ?? 0)

  return { entities, maxPageSize, error, refresh }
}

/**
 * One reading of the audit trail: the filter, the page, the walk, and the rules for whichever
 * entity is selected.
 *
 * **Every filter lives in the query the API is asked.** Nothing here narrows rows already on
 * screen — the same rule the media browser follows, and more load-bearing here: a trail is
 * append-only and only grows, so the page you are looking at is never the set you meant to search.
 *
 * **The walk is by cursor, not by offset.** `nextCursor` is the token the API hands back for the
 * next page; `skip` is never sent, because an offset makes the database read and discard everything
 * ahead of the page and shifts a row across the boundary whenever an entry arrives mid-walk. The
 * API refuses both at once with a `400`, so "send the cursor and also the offset" is not a mistake
 * that can hide.
 *
 * **Three keys, one per resource.** The listing, the declared entities, and the selected entity's
 * rules are three endpoints; `useAuditEntities` is shared with the filter bar, and the other two
 * belong to this page. What is deliberately NOT here is a second key for the same listing read a
 * second way, which is the bug `useLanguageList` documents.
 *
 * **Not a shared composable.** Like `useMediaLibrary`, this is one listing over one filter. There
 * is one Audit page, and if a second screen ever read the trail it would be reading its own
 * question, not this one.
 */
export function useAuditTrail() {
  const api = useApi()
  const { entities, maxPageSize } = useAuditEntities()

  const query = reactive<AuditQuery>({
    entityName: '',
    entityId: '',
    actor: '',
    action: '',
    from: '',
    to: '',
    correlationId: '',
    pageSize: 25
  })

  /**
   * The cursor that opened each page past the first, so "newer" is a pop rather than a second walk
   * from the top.
   *
   * A cursor walk is one-directional — the API hands back where a page ended and nothing else — so
   * going back means remembering where you came from. Emptied by every change to the question
   * below; see {@link setFilters}.
   */
  const cursors = ref<string[]>([])

  const after = computed(() => cursors.value.at(-1))

  /**
   * The two date boxes as the instants the API is asked for.
   *
   * Read as whole **UTC** days: an entry's `occurredAt` is UTC and the string is sent without a
   * zone designator, so there is one unambiguous reading of "the 24th" rather than one that
   * depends on the time zone of whichever machine the API is running on. The rows themselves are
   * drawn in local time, which `audit.filters.datesHelp` says out loud rather than leaving someone
   * to notice a late-evening entry on the edge of a range.
   */
  const bounds = computed(() => ({
    from: query.from ? `${query.from}T00:00:00` : undefined,
    to: query.to ? `${query.to}T23:59:59.999` : undefined
  }))

  const asyncData = useAsyncData<AuditPage>(
    AUDIT_TRAIL_KEY,
    (_nuxtApp, { signal }) => api.audit.query(query, { after: after.value, ...bounds.value }, { signal }),
    // `defer` for the arrivals nobody asked for — the page and its filter bar resolving in the
    // same tick — and never for a deliberate move: every call below refreshes with
    // `dedupe: 'cancel'`, because an answer already in flight is the answer to the previous
    // question. Handed back under `defer`, a filter typed while the first page was still loading
    // would quietly show the unfiltered trail and the box would say otherwise.
    { dedupe: 'defer' })

  const { data, pending, error, refresh } = asyncData

  /**
   * One entity's properties and the rule each falls under, for whichever entity is filtered on.
   *
   * `null` with no entity selected: "what is recorded for Product" is not a question with an
   * answer across entities, and the rules card draws nothing rather than something averaged.
   *
   * This is the model's own description, not a summary of the entries — which is what makes it
   * worth showing at all here. `Name` and `Description` are reported as `Ignore`, so the page can
   * say that translated text is deliberately out of the trail instead of leaving a reader to
   * conclude the recording is broken.
   */
  const schemaData = useAsyncData<AuditEntitySchema | null>(
    AUDIT_ENTITY_KEY,
    (_nuxtApp, { signal }) =>
      query.entityName ? api.audit.describe(query.entityName, { signal }) : Promise.resolve(null),
    { dedupe: 'defer' })

  const entries = computed(() => data.value?.entries ?? [])

  /** Which page of the walk is on screen. 1-based, and only ever reached one page at a time. */
  const page = computed(() => cursors.value.length + 1)

  const hasNewer = computed(() => cursors.value.length > 0)

  /**
   * Whether there is an older page to ask for.
   *
   * `nextCursor` alone is not the answer, and this is the trap in it: the API returns one for every
   * page that holds anything, the last one included — it is where the page ended, never a promise
   * that more exist. Six entries read twenty-five at a time come back with a cursor, and a pager
   * that trusted it would offer "older" on a trail it had just shown all of.
   *
   * So the other half is the page being full: fewer entries than were asked for is the end of the
   * walk. `take` cannot answer this — it reports how many entries came back, which is the same
   * number — so the comparison is against the size this page asked for, which is never above
   * `maxPageSize` because {@link setPageSize} clamps it there.
   *
   * A last page that is exactly full still offers "older" once, and the answer is an empty page
   * with "newer" beside it. That is the honest shape of a cursor walk, and the alternative is a
   * count this read deliberately never asked for.
   */
  const hasOlder = computed(() =>
    !!data.value?.nextCursor && entries.value.length >= query.pageSize)

  const isFiltered = computed(() => !!(
    query.entityName || query.entityId || query.actor || query.action
    || query.from || query.to || query.correlationId))

  /** A refusal, in the API's own words. */
  const loadError = computed(() => error.value ? problemMessage(error.value) : '')

  /** Reloads the listing for a question that has just changed. Never deferred — see above. */
  const reload = () => refresh({ dedupe: 'cancel' })

  /**
   * Changes the question and starts the walk again from the newest entry.
   *
   * **Emptying `cursors` is the whole of it, and it is not optional.** A cursor is a position in
   * one ordered, filtered trail. Kept across a filter change it addresses a place in a trail that
   * is no longer being read: the API is handed `after` from the old walk together with the new
   * filter, and answers with entries from the middle of a set the user never asked for — or with
   * nothing at all, which reads as "no entries match" for a filter that matches plenty.
   */
  function setFilters(patch: Partial<AuditQuery>) {
    Object.assign(query, patch)
    cursors.value = []

    return reload()
  }

  /** Asks for the next page, from where this one ended. */
  function older() {
    const cursor = data.value?.nextCursor

    if (!cursor) {
      return
    }

    cursors.value = [...cursors.value, cursor]

    return reload()
  }

  /** Back to the page before, by the cursor that opened it. */
  function newer() {
    if (!hasNewer.value) {
      return
    }

    cursors.value = cursors.value.slice(0, -1)

    return reload()
  }

  /**
   * How many entries a page asks for, bounded by what the API says it will serve.
   *
   * Clamped here as well as on the server because a control offering a number that is then
   * silently reduced is a control that lies — the response's `take` would disagree with the box.
   */
  function setPageSize(size: number) {
    const max = maxPageSize.value || size

    return setFilters({ pageSize: Math.max(1, Math.min(size, max)) })
  }

  /**
   * Narrows the trail to one edit — every entry written under the same correlation id.
   *
   * Worth a control of its own because an edit in this application is not always one entry: a
   * product created with text in three languages writes the product's entry and, in a scope of its
   * own, the creation trail's — see `Composition/CreationTrail.cs`. The correlation id is what says
   * those belong together, and reading them apart is how someone concludes the trail is recording
   * twice.
   *
   * The other filters are cleared with it: the rest of an edit may well be another entity.
   */
  function showEdit(correlationId: string) {
    return setFilters({
      correlationId,
      entityName: '',
      entityId: '',
      actor: '',
      action: '',
      from: '',
      to: ''
    })
  }

  function reset() {
    return setFilters({
      entityName: '',
      entityId: '',
      actor: '',
      action: '',
      from: '',
      to: '',
      correlationId: ''
    })
  }

  /** The rules follow the entity filter, and nothing else re-reads them. */
  watch(() => query.entityName, () => schemaData.refresh({ dedupe: 'cancel' }))

  return {
    query, entries, entities, maxPageSize, schema: schemaData.data,
    pending, page, hasNewer, hasOlder, isFiltered, loadError,
    setFilters, setPageSize, older, newer, showEdit, reset, reload
  }
}
