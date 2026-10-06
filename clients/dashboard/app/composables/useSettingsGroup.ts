import type { ProblemDetails, SettingGroup, SettingValues } from '~/types'

/**
 * Loads and saves one settings group.
 *
 * The form fields are whatever `GET api/settings/{route}` returns, so a property added to
 * the C# class shows up without changes here. `POST` replaces the whole group, so every
 * property is sent back on save.
 *
 * Saves are conditional. `GET` returns an `ETag` naming the revision that was loaded, and the
 * `POST` sends it back as `If-Match`; if someone else saved in between, the API answers
 * `412 Precondition Failed` and nothing is written. Without that, whoever saved last would
 * silently revert the other person's edit.
 */
export function useSettingsGroup(group: SettingGroup) {
  const toast = useToast()
  const { notifySuccess, notifyProblem } = useNotify()
  const { t } = useI18n()
  const language = useLanguage()
  const endpoint = `/api/settings/${group.route}`

  /**
   * The reading language, sent on every call to this group.
   *
   * The settings library does not read it — but `UseMultiLingual()` is global middleware, so it
   * resolves the language for these requests too, and the API's language-aware cache keys each
   * group's entry by it. Without the header every request would resolve to the default and share
   * one entry.
   */
  const languageHeaders = () => ({ 'Accept-Language': language.value })

  /** Last known server state — the baseline for dirty checking and the save payload. */
  const values = shallowRef<SettingValues>({})
  /** Revision the form was loaded at. Sent as `If-Match` so a stale save is refused. */
  const etag = ref<string | null>(null)
  // Form state. The properties differ per group and each control narrows the value at the
  // point of use, so this is deliberately untyped rather than a union the template must cast.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const state = reactive<Record<string, any>>({})
  const keys = ref<string[]>([])

  const pending = ref(true)
  const saving = ref(false)
  const loadError = ref('')
  const problem = ref('')
  const errors = ref<Record<string, string>>({})
  /** Set when the last save was refused because the group moved on underneath us. */
  const conflict = ref(false)

  const changedKeys = () => keys.value.filter(key => state[key] !== values.value[key])
  const dirty = computed(() => changedKeys().length > 0)

  /**
   * Reads the group and adopts it as the baseline: its values, its revision, and its property list,
   * which is what the form draws its fields from.
   *
   * `keep` names the fields to leave as the user has them. Empty on a plain load, where the server
   * is the only thing on screen worth showing; on the retry after a conflict it is the fields this
   * user actually edited, so the other writer's changes survive and this one's are re-applied over
   * them.
   */
  async function adoptServerState(keep: string[] = []) {
    // `.raw` rather than `$fetch` so the ETag header is reachable, not just the body.
    const response = await $fetch.raw<SettingValues>(endpoint, { headers: languageHeaders() })
    const next = response._data ?? {}

    etag.value = response.headers.get('etag')
    values.value = next
    keys.value = Object.keys(next)

    for (const key of keys.value) {
      if (!keep.includes(key)) {
        state[key] = next[key]
      }
    }
  }

  async function load() {
    pending.value = true
    loadError.value = ''

    try {
      await adoptServerState()
      conflict.value = false
    } catch (error) {
      loadError.value = problemMessage(error)
    } finally {
      pending.value = false
    }
  }

  /** True when the group was written; false when it was refused or failed. */
  async function save() {
    saving.value = true
    clearErrors()

    try {
      await $fetch(endpoint, {
        method: 'POST',
        body: { ...values.value, ...state },
        headers: {
          ...languageHeaders(),
          // Omitted on the very first save of a group that has never been read successfully;
          // the API then writes unconditionally, which is the same behaviour as before.
          ...(etag.value ? { 'If-Match': etag.value } : {})
        }
      })
      await load()

      notifySuccess(
        t('settings.form.toast.saved', { group: groupLabel(group.route, group.name) }),
        t('settings.form.toast.savedDescription')
      )

      return true
    } catch (error) {
      if (statusOf(error) === 412) {
        conflict.value = true
        problem.value = t('settings.form.toast.conflictProblem')

        toast.add({
          title: t('settings.form.toast.conflictTitle'),
          description: t('settings.form.toast.conflictDescription'),
          icon: 'i-lucide-git-merge',
          color: 'warning'
        })
      } else {
        applyProblem(error)

        // `problem.value` rather than the error: applyProblem has just split the rejection into
        // per-field messages and whatever was left over, and the leftovers are what to show here.
        notifyProblem(t('settings.form.toast.saveFailed'), problem.value)
      }
    } finally {
      saving.value = false
    }

    return false
  }

  /**
   * Recovery path after a conflict: adopt whatever is on the server now, re-apply only the
   * fields this user actually edited, and save against the fresh revision. Fields the other
   * writer changed and this user did not are kept, rather than being clobbered.
   */
  async function retryOnLatest() {
    // Read before the fetch: adopting the server's state is what makes them stop being "changed".
    const mine = changedKeys()
    saving.value = true

    try {
      await adoptServerState(mine)
    } catch (error) {
      applyProblem(error)
      return
    } finally {
      saving.value = false
    }

    return save()
  }

  function reset() {
    clearErrors()
    for (const key of keys.value) {
      state[key] = values.value[key]
    }
  }

  function clearErrors() {
    problem.value = ''
    errors.value = {}
  }

  /** Turns a rejected POST into per-field messages, keeping any that match no field. */
  function applyProblem(error: unknown) {
    const data = (error as { data?: ProblemDetails | string })?.data

    if (typeof data === 'string') {
      problem.value = data
      return
    }

    if (!data?.errors) {
      problem.value = data?.detail || problemMessage(error)
      return
    }

    const unmatched: string[] = []
    const matched: Record<string, string> = {}

    for (const [property, messages] of Object.entries(data.errors)) {
      // FluentValidation reports PascalCase property names; the JSON payload is camelCase.
      const key = keys.value.find(candidate => candidate.toLowerCase() === property.toLowerCase())
      const message = messages.join(' ')

      if (key) {
        matched[key] = message
      } else {
        unmatched.push(`${property}: ${message}`)
      }
    }

    errors.value = matched

    // `data.title` first: when the API named the problem, those are its words, and only the
    // generic stand-in for "it said nothing" is ours to translate.
    problem.value = [data.title || t('settings.form.rejected'), ...unmatched].join(' ')
  }

  // Clear stale server errors as soon as the user edits anything. A conflict is deliberately
  // left standing: it describes the server, not the form, so editing does not resolve it.
  watch(() => keys.value.map(key => state[key]), () => {
    if (!conflict.value && (problem.value || Object.keys(errors.value).length)) {
      clearErrors()
    }
  })

  onMounted(load)

  return {
    keys, state, values, pending, saving, dirty, loadError, problem, errors, conflict,
    load, save, reset, retryOnLatest
  }
}
