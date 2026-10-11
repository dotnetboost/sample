import type { Language, LanguageUpdate, NewLanguage, PurgeResult } from '~/types'

/**
 * The language list, plus the administration calls `MapLanguageEndpoints()` maps. Lifted from the
 * DotNetBoost.MultiLingual dashboard, i18n layer and all.
 *
 * The list itself is `useLanguageList` — one `useAsyncData` entry shared with every other reader
 * of it — so each write here ends by refreshing that one entry and the header selector, the
 * catalogue's editors and the overview's count all follow. This composable used to fetch the list
 * into a ref of its own, which is why they did not.
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
export function useLanguages() {
  const list = useLanguageList()
  const { notifySuccess, notifyFailure } = useNotify()
  const { t } = useI18n()

  /** Code of the language an action is running on, for per-row loading states. */
  const busy = ref<string | null>(null)

  /** Runs one call, reports the outcome, and reloads. Resolves to the result, or `undefined` if it failed. */
  async function run<T>(
    code: string,
    call: () => Promise<T>,
    success: (result: T) => { title: string, description?: string }
  ) {
    busy.value = code

    try {
      const result = await call()
      const { title, description } = success(result)
      notifySuccess(title, description)
      return result
    } catch (error) {
      notifyFailure(t('api.refused'), error)
      return undefined
    } finally {
      busy.value = null

      // `cancel`, against the entry's own `defer`: this read has to be newer than the write that
      // just landed, and deferring would be allowed to answer it with a GET that was already in
      // flight before the POST — the list as it was a moment ago, which is the thing being fixed.
      await list.refresh({ dedupe: 'cancel' })
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

  return { ...list, busy, create, update, setDefault, remove, purge }
}
