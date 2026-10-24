/**
 * Who the dashboard was last acting as, across visits.
 *
 * `localStorage`, beside the reading language and for the same reason: nothing on the server wants
 * it. `X-Sample-Actor` is attached per request by `useApi` from the live value — a cookie would be
 * a second copy for the Nitro proxy to read and disagree with.
 *
 * **It is not a credential, and remembering it is not "staying signed in".** The API's scheme
 * trusts the header outright; see `Authentication/SampleActorAuthentication.cs`, which says so at
 * length. What is kept here is the answer to "whose name should the trail carry", so that reloading
 * the page does not quietly go back to recording changes against nobody.
 */
const STORAGE_KEY = 'dotnetboost-sample:acting-as'

/**
 * Guarded like {@link readRememberedLanguage}: a private window, blocked site data and a cleared
 * profile each throw or answer `null`, and none of them is a reason for the dashboard to refuse to
 * open. It starts out acting as nobody, which is a state the whole app already handles.
 */
export function readRememberedActor(): string {
  try {
    return localStorage.getItem(STORAGE_KEY)?.trim() ?? ''
  } catch {
    return ''
  }
}

/** Stores the name, or forgets it when it is empty — "acting as nobody" is not a name to keep. */
export function rememberActor(actor: string): void {
  try {
    if (actor) {
      localStorage.setItem(STORAGE_KEY, actor)
    } else {
      localStorage.removeItem(STORAGE_KEY)
    }
  } catch {
    // The choice still holds for this session; it just will not outlive it.
  }
}
