/**
 * Deleting a category or a product, behind a confirmation: what is about to go, whether it is
 * going, and the call that does it.
 *
 * Both toasts carry the API's own sentence. A success names the row by slug or SKU, in the
 * language the list is being read in; a refusal — "this category still has products" — arrives as
 * problem details with a localized `detail`, which `notifyFailure` reads.
 */
export function useCatalogDelete(
  remove: (id: number) => Promise<{ message: string }>,
  onDeleted: () => unknown
) {
  const { t } = useI18n()
  const { notifySuccess, notifyFailure } = useNotify()

  const confirming = ref<{ id: number, name: string } | null>(null)
  const deleting = ref(false)

  async function confirm() {
    if (!confirming.value) return

    deleting.value = true
    try {
      const { message } = await remove(confirming.value.id)
      await onDeleted()

      notifySuccess(t('catalog.toast.deleted'), message)
      confirming.value = null
    } catch (error) {
      notifyFailure(t('catalog.toast.deleteFailed'), error)
    } finally {
      deleting.value = false
    }
  }

  return { confirming, deleting, confirm }
}
