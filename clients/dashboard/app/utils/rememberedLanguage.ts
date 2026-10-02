import type { TextDirection } from '~/types'

/**
 * What the dashboard remembers about the language you were last reading in.
 *
 * `localStorage` rather than a cookie: nothing on the server wants it. This is a client-rendered
 * SPA, and `Accept-Language` is attached per request by `useApi` from the live value, not from
 * anything a server reads off a cookie jar.
 *
 * **Why the direction is stored too.** The language list is what says which way a language reads,
 * and it arrives over the network. Remembering only the code would open the next visit in Arabic
 * text laid out left-to-right until `GET api/languages` answered, and then jump. So the API's
 * previous answer is cached beside the code — a cache of what the API said, not a list of RTL
 * codes written down here, which is the thing this dashboard deliberately does not have. A
 * language whose direction is changed on the Languages page corrects itself on the next load,
 * because the live answer always wins over this one.
 */
export interface RememberedLanguage {
  code: string
  direction: TextDirection
}

const STORAGE_KEY = 'dotnetboost-sample:reading-language'

/**
 * Every access is guarded. A private window, blocked site data, a cleared profile and a full quota
 * all throw or answer `null` rather than failing politely, and not one of them is a reason for the
 * dashboard to refuse to open — it just starts in the default language, exactly as it used to.
 */
export function readRememberedLanguage(): RememberedLanguage | null {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)

    if (!stored) {
      return null
    }

    const parsed = JSON.parse(stored) as Partial<RememberedLanguage>

    // Written by an older build, or by hand: anything that is not the shape expected here is
    // discarded rather than half-used.
    return typeof parsed?.code === 'string' && parsed.code
      ? { code: parsed.code, direction: parsed.direction === 'rtl' ? 'rtl' : 'ltr' }
      : null
  } catch {
    return null
  }
}

export function rememberLanguage(value: RememberedLanguage): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(value))
  } catch {
    // Nothing to do, and nothing worth telling the user: the choice still holds for this session,
    // it just will not outlive it.
  }
}
