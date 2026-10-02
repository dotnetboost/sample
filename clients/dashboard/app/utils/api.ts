import type { ProblemDetails } from '~/types'

/** ofetch surfaces the HTTP status differently depending on where the failure happened. */
export function statusOf(error: unknown): number | undefined {
  const e = error as { status?: number, statusCode?: number, response?: { status?: number } }
  return e?.status ?? e?.statusCode ?? e?.response?.status
}

/**
 * The text to show for a failed call.
 *
 * The API answers with problem details, and its `detail` is written for the caller — "The upload
 * is 52,428,800 bytes; the limit is 26,214,400." — so it is shown as it is rather than replaced
 * with something vaguer of our own. That is also why only the last line here is translated: every
 * line above it is the API talking, and the dashboard does not put words in its mouth.
 */
export function problemMessage(error: unknown): string {
  const data = (error as { data?: ProblemDetails | string })?.data

  if (typeof data === 'string' && data) {
    return data
  }

  if (data && typeof data === 'object') {
    if (data.errors) {
      const messages = Object.values(data.errors).flat()
      if (messages.length) {
        return messages.join(' ')
      }
    }

    if (data.detail || data.title) {
      return data.detail || data.title!
    }
  }

  return (error as Error)?.message || uiText('api.unreachable', 'The API could not be reached.')
}
