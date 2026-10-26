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
 * One reading of the audit trail: the filter, the page, and the rules for whichever entity is
 * selected.
 *
 * **Every filter lives in the query the API is asked.** Nothing here narrows rows already on
 * screen — the same rule the media browser follows, and more load-bearing here: a trail is
 * append-only and only grows, so the page you are looking at is never the set you meant to search.
 *
 * **Pages are numbered, by offset and a count.** Each read sends `skip` and asks for the total, so
 * the pager can show page numbers and jump to any of them. That is the costlier of the API's two
 * ways to page — it counts the matches on every read, and the database reads and discards
 * everything ahead of the page — and it is chosen anyway because "page 3 of 7" is what people
 * expect from a table. An entry recorded between two reads shifts the rows down by one, so a row
 * can appear at the bottom of one page and again at the top of the next. `after` is never sent:
 * the API refuses it together with `skip`.
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

  /** Which page is on screen, 1-based. Back to 1 on every change to the question; see {@link setFilters}. */
  const page = ref(1)

  const skip = computed(() => (page.value - 1) * query.pageSize)

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
    (_nuxtApp, { signal }) => api.audit.query(query, { skip: skip.value, ...bounds.value }, { signal }),
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

  /** How many entries match, across every page. 0 until the first answer lands. */
  const total = computed(() => data.value?.total ?? 0)

  const isFiltered = computed(() => !!(
    query.entityName || query.entityId || query.actor || query.action
    || query.from || query.to || query.correlationId))

  /** A refusal, in the API's own words. */
  const loadError = computed(() => error.value ? problemMessage(error.value) : '')

  /** Reloads the listing for a question that has just changed. Never deferred — see above. */
  const reload = () => refresh({ dedupe: 'cancel' })

  /**
   * Changes the question and goes back to the first page.
   *
   * Back to page 1 because the old page number belongs to the old question: page 4 of the whole
   * trail can be past the end of a filter that matches one page, and the API answers that with
   * nothing at all, which reads as "no entries match" for a filter that matches plenty.
   */
  function setFilters(patch: Partial<AuditQuery>) {
    Object.assign(query, patch)
    page.value = 1

    return reload()
  }

  /** Moves to another page of the same question. */
  function goTo(target: number) {
    page.value = target

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
   * Worth a control of its own because an edit in this application is not always one entry: the
   * API's capture records every entity a save touches, and one save can touch several — a settings
   * group writes a row per value, and a product's images are rows of DotNetBoost.Media's own. The
   * correlation id is one per save, which is what says those belong together, and reading them
   * apart is how someone concludes the trail is recording twice.
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
    pending, page, total, isFiltered, loadError,
    setFilters, setPageSize, goTo, showEdit, reset, reload
  }
}
