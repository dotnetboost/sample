import { createSharedComposable } from '@vueuse/core'
import type { Language, LanguageUpdate, NewLanguage, PurgeResult } from '~/types'

/**
 * The language list from `GET api/languages`, shared by every page, and the administration calls
 * `MapLanguageEndpoints()` maps. Lifted from the DotNetBoost.MultiLingual dashboard, with that
 * dashboard's i18n layer replaced by plain strings — this sample does not translate its own chrome.
 *
 * Each call reloads the list afterwards: setting a default demotes another language, so patching
 * one row locally would be wrong.
 *
 * Refusals come back as problem details — the default cannot be deleted or deactivated, a language
 * with content cannot be deleted — and are shown as the API words them, rather than replaced with
 * something vaguer here.
 */
const _useLanguages = () => {
  const toast = useToast()
  const { baseLanguage } = useRuntimeConfig().public

  const languages = ref<Language[]>([])
  const pending = ref(false)
  const loaded = ref(false)
  const loadError = ref('')
  /** Code of the language an action is running on, for per-row loading states. */
  const busy = ref<string | null>(null)

  const active = computed(() => languages.value.filter(language => language.isActive))
  const defaultLanguage = computed(() => languages.value.find(language => language.isDefault))

  async function load() {
    pending.value = true
    loadError.value = ''

    try {
      languages.value = await $fetch<Language[]>('/api/languages')
      loaded.value = true
    } catch (error) {
      loadError.value = problemMessage(error)
    } finally {
      pending.value = false
    }
  }

  /** Loads the list the first time a page needs it. */
  async function ensureLoaded() {
    if (!loaded.value && !pending.value) {
      await load()
    }
  }

  function find(code: string) {
    return languages.value.find(language => language.code.toLowerCase() === code.toLowerCase())
  }

  /**
   * Whether `code` is `MultiLingualOptions.BaseLanguage` — the language kept in the entity's own
   * columns rather than in the translations table.
   *
   * This sample does not configure one, so it is always false and the badge never shows. Left in
   * because the mode exists and the page would be wrong without it the moment someone turns it on.
   */
  function isBase(code: string) {
    return !!baseLanguage && code.toLowerCase() === String(baseLanguage).toLowerCase()
  }

  /** Runs one call, reports the outcome, and reloads. Resolves to the result, or `undefined` if it failed. */
  async function run<T>(
    code: string,
    call: () => Promise<T>,
    success: (result: T) => { title: string, description?: string }
  ) {
    busy.value = code

    try {
      const result = await call()
      toast.add({ ...success(result), icon: 'i-lucide-check', color: 'success' })
      return result
    } catch (error) {
      toast.add({
        title: 'The API refused that',
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

  const create = (input: NewLanguage) => run(input.code, () =>
    $fetch<Language>('/api/languages', { method: 'POST', body: input }),
  created => ({
    title: `${created.name} added`,
    description: created.isDefault
      ? `${created.code} is now the default — content falls back to it.`
      : `Content can now be written in ${created.code}.`
  }))

  const update = (code: string, input: LanguageUpdate) => run(code, () =>
    $fetch<Language>(`/api/languages/${code}`, { method: 'PUT', body: input }),
  updated => ({
    title: `${updated.name} saved`,
    description: updated.isActive ? undefined : 'It is inactive, so content is no longer served in it.'
  }))

  const setDefault = (code: string) => run(code, () =>
    $fetch<Language>(`/api/languages/${code}/set-default`, { method: 'POST' }),
  promoted => ({
    title: `${promoted.name} is now the default`,
    description: 'Missing translations fall back to it, and the language that held the role was demoted.'
  }))

  const remove = (code: string) => run(code, () =>
    $fetch(`/api/languages/${code}`, { method: 'DELETE' }),
  () => ({ title: `${code} deleted` }))

  const purge = (code: string) => run(code, () =>
    $fetch<PurgeResult>(`/api/languages/${code}/translations`, { method: 'DELETE' }),
  result => ({
    title: `${result.deletedTranslations} translation${result.deletedTranslations === 1 ? '' : 's'} removed`,
    description: `${result.code} still exists; everything written in it is gone.`
  }))

  return {
    languages, active, defaultLanguage, pending, loaded, loadError, busy,
    load, ensureLoaded, find, isBase, create, update, setDefault, remove, purge
  }
}

export const useLanguages = createSharedComposable(_useLanguages)
