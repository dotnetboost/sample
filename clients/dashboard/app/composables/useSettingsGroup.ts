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

  async function load() {
    pending.value = true
    loadError.value = ''

    try {
      // `.raw` rather than `$fetch` so the ETag header is reachable, not just the body.
      const response = await $fetch.raw<SettingValues>(endpoint, { headers: languageHeaders() })
      const next = response._data ?? {}

      etag.value = response.headers.get('etag')
      conflict.value = false
      values.value = next
      keys.value = Object.keys(next)
      for (const key of keys.value) {
        state[key] = next[key]
      }
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

      toast.add({
        title: `${group.label} saved`,
        description: 'The new values are live — no redeploy needed.',
        icon: 'i-lucide-check',
        color: 'success'
      })

      return true
    } catch (error) {
      if (statusOf(error) === 412) {
        conflict.value = true
        problem.value = 'Someone else changed these settings while you were editing. '
          + 'Nothing was saved, and your edits are still here.'

        toast.add({
          title: 'Save refused — settings changed elsewhere',
          description: 'Reload the latest values, or re-apply your edits on top of them.',
          icon: 'i-lucide-git-merge',
          color: 'warning'
        })
      } else {
        applyProblem(error)

        toast.add({
          title: 'Save failed',
          description: problem.value,
          icon: 'i-lucide-triangle-alert',
          color: 'error'
        })
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
    const mine = changedKeys()
    saving.value = true

    try {
      const response = await $fetch.raw<SettingValues>(endpoint, { headers: languageHeaders() })
      const latest = response._data ?? {}

      etag.value = response.headers.get('etag')
      values.value = latest
      keys.value = Object.keys(latest)

      for (const key of keys.value) {
        if (!mine.includes(key)) {
          state[key] = latest[key]
        }
      }
    } catch (error) {
      applyProblem(error)
      saving.value = false
      return
    }

    saving.value = false
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

    problem.value = [data.title || 'The server rejected these values.', ...unmatched].join(' ')
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
