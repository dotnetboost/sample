import type { MediaFile, MediaPage, MediaQuery, MediaSortBy, MediaUpdate } from '~/types'

export interface MediaLibraryOptions {
  /** Restricts every listing to one entity's files. Used by the picker's "attached" tab. */
  owner?: Ref<{ entityName: string, entityId: string } | undefined>
  /** Rows per page. Default 12, which fills the grid at three and four columns alike. */
  pageSize?: number
}

/**
 * One media listing: its filter, its page, and the calls that change what is in it.
 *
 * Deliberately NOT a shared composable. The Media page and the picker modal on the products page
 * are the same browser over two independent queries, and sharing state between them would make
 * opening the picker clobber whatever the page behind it was showing.
 *
 * Every filter lives in the query sent to the API rather than being applied to the rows already
 * loaded. That is the whole reason the API grew `search` and `sortBy`: filtering a page in the
 * browser searches one page, which is wrong the moment there is a second one.
 */
export function useMediaLibrary(options: MediaLibraryOptions = {}) {
  const toast = useToast()

  const query = reactive<MediaQuery>({
    search: '',
    category: '',
    tag: '',
    contentType: '',
    sortBy: 'createdAt',
    descending: true,
    page: 1,
    pageSize: options.pageSize ?? 12
  })

  const items = ref<MediaFile[]>([])
  const total = ref(0)
  const pending = ref(false)
  const loadError = ref('')
  /** Id of the file an action is running on, for per-row loading states. */
  const busy = ref<string | null>(null)

  /**
   * Category and tag values seen so far, offered as suggestions in the filter comboboxes.
   *
   * Gathered from the rows that have been loaded rather than from the API, which has no endpoint
   * for distinct values. That makes them a convenience, not a complete list — both filters accept
   * free text, so a value that has not been seen yet can still be typed.
   */
  const knownCategories = ref<Set<string>>(new Set())
  const knownTags = ref<Set<string>>(new Set())

  const categoryOptions = computed(() => [...knownCategories.value].sort())
  const tagOptions = computed(() => [...knownTags.value].sort())

  const pageCount = computed(() => Math.max(1, Math.ceil(total.value / query.pageSize)))
  const isFiltered = computed(() => !!(query.search || query.category || query.tag || query.contentType))

  async function load() {
    pending.value = true
    loadError.value = ''

    try {
      const owner = options.owner?.value

      const page = await $fetch<MediaPage>('/api/media', {
        query: {
          search: query.search || undefined,
          category: query.category || undefined,
          tag: query.tag || undefined,
          contentType: query.contentType || undefined,
          ownerEntityName: owner?.entityName,
          ownerEntityId: owner?.entityId,
          role: query.role || undefined,
          unattached: query.unattached || undefined,
          sortBy: query.sortBy,
          descending: query.descending,
          skip: (query.page - 1) * query.pageSize,
          take: query.pageSize
        }
      })

      items.value = page.items
      total.value = page.total

      for (const file of page.items) {
        if (file.category) knownCategories.value.add(file.category)
        for (const tag of file.tags) knownTags.value.add(tag)
      }

      // A page that no longer exists — the last file on it was deleted, or the filter narrowed —
      // would otherwise show an empty grid with a pager pointing past the end.
      if (!page.items.length && query.page > 1 && total.value > 0) {
        query.page = Math.min(query.page, pageCount.value)
        await load()
      }
    } catch (error) {
      loadError.value = problemMessage(error)
      items.value = []
      total.value = 0
    } finally {
      pending.value = false
    }
  }

  /** Runs one call, reports the outcome, and reloads. Resolves to the result, or `undefined` if it failed. */
  async function run<T>(id: string | null, call: () => Promise<T>, success: (result: T) => string, failure: string) {
    busy.value = id

    try {
      const result = await call()
      toast.add({ title: success(result), icon: 'i-lucide-check', color: 'success' })
      return result
    } catch (error) {
      toast.add({
        title: failure,
        description: problemMessage(error),
        icon: 'i-lucide-triangle-alert',
        color: 'error'
      })
      return undefined
    } finally {
      busy.value = null
      await load()
    }
  }

  /**
   * Uploads one file. The body is `FormData`, so the browser sets the multipart boundary and the
   * bytes stream out — reading the file into memory first would defeat the streaming the whole
   * upload path is built around.
   */
  function upload(input: {
    file: File
    category?: string
    tags?: string[]
    uploadedBy?: string
    owner?: { entityName: string, entityId: string }
    /** What the owner uses it for. Only meaningful with an owner, which does the attaching. */
    role?: string
  }) {
    const body = new FormData()
    body.append('file', input.file, input.file.name)

    if (input.category) body.append('category', input.category)
    if (input.tags?.length) body.append('tags', input.tags.join(','))
    if (input.uploadedBy) body.append('uploadedBy', input.uploadedBy)

    // With an owner the API uploads and attaches in one request. Without one the file lands in the
    // library unattached, waiting to be claimed — which is what the Media page does.
    if (input.owner) {
      body.append('ownerEntityName', input.owner.entityName)
      body.append('ownerEntityId', input.owner.entityId)
      if (input.role) body.append('role', input.role)
    }

    return run(
      null,
      () => $fetch<MediaFile>('/api/media', { method: 'POST', body }),
      uploaded => `Uploaded ${uploaded.fileName}`,
      'Upload refused'
    )
  }

  const update = (id: string, patch: MediaUpdate) => run(
    id,
    () => $fetch<MediaFile>(`/api/media/${id}`, { method: 'PATCH', body: patch }),
    updated => `Saved ${updated.fileName}`,
    'Could not save the file'
  )

  /**
   * Attaches a file to an entity. A link of its own, not a field on the file — so this adds the
   * entity to whoever is already using the file rather than taking it off them.
   *
   * `replace` takes a single-valued role over from whatever held it; without it the file is
   * appended, as a gallery image is.
   */
  const attach = (
    id: string,
    owner: { entityName: string, entityId: string },
    role?: string,
    replace = false
  ) => run(
    id,
    () => $fetch<MediaFile>(`/api/media/${id}/attachments`, {
      method: 'POST',
      body: { ownerEntityName: owner.entityName, ownerEntityId: owner.entityId, role, replace }
    }),
    attached => `Attached ${attached.fileName}`,
    'Could not attach the file'
  )

  /**
   * Removes one link, addressed by its own id — which `GET api/media/{id}` hands back with the file.
   * The file stays in the library, and stays attached to every other entity using it: this is "take
   * it off this product", not "delete it".
   */
  const detach = (id: string, attachmentId: string) => run(
    id,
    () => $fetch<MediaFile>(`/api/media/${id}/attachments/${attachmentId}`, { method: 'DELETE' }),
    detached => `Detached ${detached.fileName}`,
    'Could not detach the file'
  )

  /**
   * One file with its attachments, which a listing does not carry. Used when the details of a
   * single file are opened, and before offering to delete one — a delete takes the file out of
   * every entity using it, and the count is what makes that visible.
   */
  const readOne = (id: string) => $fetch<MediaFile>(`/api/media/${id}`)

  /** Saves the order one entity put a role's files in. */
  const reorder = (
    owner: { entityName: string, entityId: string },
    role: string,
    mediaFileIds: string[]
  ) => run(
    null,
    () => $fetch('/api/media/attachments/order', {
      method: 'PUT',
      body: { ownerEntityName: owner.entityName, ownerEntityId: owner.entityId, role, mediaFileIds }
    }),
    () => 'Order saved',
    'Could not save the order'
  )

  const remove = (file: MediaFile) => run(
    file.id,
    () => $fetch(`/api/media/${file.id}`, { method: 'DELETE' }),
    () => `Deleted ${file.fileName}`,
    'Could not delete the file'
  )

  function reset() {
    query.search = ''
    query.category = ''
    query.tag = ''
    query.contentType = ''
    query.page = 1
  }

  function sortBy(column: MediaSortBy) {
    // Clicking the column already sorted on reverses it; a different column starts descending,
    // which is what "newest", "largest" and "Z first" all mean.
    if (query.sortBy === column) {
      query.descending = !query.descending
    } else {
      query.sortBy = column
      query.descending = true
    }
  }

  // Changing the filter or the sort returns to page one and reloads.
  //
  // Page one for both: a narrowed filter can leave fewer pages than the one being viewed, and a
  // new ordering makes the old position meaningless — staying on page 2 drops the user into the
  // middle of a list they just asked to see differently.
  watch(
    () => [query.search, query.category, query.tag, query.contentType, query.sortBy, query.descending],
    () => {
      if (query.page === 1) {
        load()
      } else {
        // The page watcher below does the reload.
        query.page = 1
      }
    }
  )

  watch(() => [query.page, query.pageSize], load)

  if (options.owner) {
    watch(options.owner, load, { deep: true })
  }

  return {
    query, items, total, pending, loadError, busy,
    pageCount, isFiltered, categoryOptions, tagOptions,
    load, upload, update, attach, detach, readOne, reorder, remove, reset, sortBy
  }
}
