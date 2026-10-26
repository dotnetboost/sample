import { toast } from 'vue-sonner'

/**
 * The two toasts this dashboard actually raises: something worked, something was refused.
 *
 * Both shapes were written out at seventeen call sites, which made the icon and the colour part of
 * every caller's business and let them drift — three of them described a failure with
 * `String(error)` and so showed `FetchError: [DELETE] "/api/categories/3": 409` where the API had
 * sent a sentence written for the reader. The shape belongs in one place; the words stay with the
 * caller, which is the only part it knows.
 *
 * **What is deliberately NOT here.** A toast that is neither of these two — the save conflict, with
 * its own colour and icon because a refused write is not a failed one, or a client-side validation
 * message with no API failure behind it at all — stays a plain call on vue-sonner's `toast`. Three helpers covering
 * every case would mean every exception reading as the rule.
 */
export function useNotify() {
  /** Something worked. */
  function notifySuccess(title: string, description?: string) {
    toast.success(title, { description })
  }

  /**
   * Something was refused or failed, described in the API's own words — `problemMessage` reads the
   * problem details, which are already written for the caller and already in their language.
   */
  function notifyFailure(title: string, error: unknown) {
    notifyProblem(title, problemMessage(error))
  }

  /**
   * The same toast for a caller that has already read the message out of the failure — the settings
   * form, which splits one rejection into per-field errors and a remainder.
   */
  function notifyProblem(title: string, description: string) {
    toast.error(title, { description })
  }

  return { notifySuccess, notifyFailure, notifyProblem }
}
