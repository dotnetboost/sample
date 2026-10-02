import { createSharedComposable } from '@vueuse/core'
import type { Language, LanguageUpdate, NewLanguage, PurgeResult } from '~/types'

/**
 * The language list from `GET api/languages`, shared by every page, and the administration calls
 * `MapLanguageEndpoints()` maps. Lifted from the DotNetBoost.MultiLingual dashboard, i18n layer
 * and all.
 *
 * Note what is translated and what is not. The outcome a toast reports — "Arabic saved" — is this
 * dashboard's own words, so it comes from the message bundles. A refusal is the API's words, and
 * is shown as the API wrote them: the problem detail is already written for the caller, and
 * replacing it with something of ours would only make it vaguer.
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
  const { t } = useI18n()
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
        title: t('api.refused'),
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
    title: t('languages.toast.added', { name: created.name }),
    description: created.isDefault
      ? t('languages.toast.addedDefault', { code: created.code })
      : t('languages.toast.addedOther', { code: created.code })
  }))

  const update = (code: string, input: LanguageUpdate) => run(code, () =>
    $fetch<Language>(`/api/languages/${code}`, { method: 'PUT', body: input }),
  updated => ({
    title: t('languages.toast.saved', { name: updated.name }),
    description: updated.isActive ? undefined : t('languages.toast.savedInactive')
  }))

  const setDefault = (code: string) => run(code, () =>
    $fetch<Language>(`/api/languages/${code}/set-default`, { method: 'POST' }),
  promoted => ({
    title: t('languages.toast.promoted', { name: promoted.name }),
    description: t('languages.toast.promotedDescription')
  }))

  const remove = (code: string) => run(code, () =>
    $fetch(`/api/languages/${code}`, { method: 'DELETE' }),
  () => ({ title: t('languages.toast.deleted', { code }) }))

  const purge = (code: string) => run(code, () =>
    $fetch<PurgeResult>(`/api/languages/${code}/translations`, { method: 'DELETE' }),
  result => ({
    // Counted, so Arabic gets its dual and its three-to-ten form rather than an English plural -s.
    title: t('languages.toast.purged', result.deletedTranslations),
    description: t('languages.toast.purgedDescription', { code: result.code })
  }))

  return {
    languages, active, defaultLanguage, pending, loaded, loadError, busy,
    load, ensureLoaded, find, isBase, create, update, setDefault, remove, purge
  }
}

export const useLanguages = createSharedComposable(_useLanguages)
