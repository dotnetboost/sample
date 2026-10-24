/**
 * Proxies every `/api/**` call through to the sample API — the four libraries' endpoints and the
 * application's own alike, which is why all of them live under `/api` on the server.
 *
 * The browser therefore only ever talks to this app's own origin, and the target moves between
 * environments by changing `NUXT_API_URL` alone. An upload streams through Nitro rather than
 * being buffered here: `proxyRequest` forwards the request body as it arrives.
 *
 * ## Who the API thinks is calling
 *
 * DotNetBoost.Auditing's endpoints require an authenticated caller — the library's own default,
 * which this sample keeps — and the API's stand-in scheme reads the caller's name off
 * `X-Sample-Actor`. So something had to decide what the browser sends. There were two honest
 * answers, and this one is the second:
 *
 *   • **the proxy could add the header itself**, forwarding one name the dashboard knows about. It
 *     is one line here and it is rejected: the dashboard does not know about anybody. The name
 *     would have to be made up — `dashboard@localhost`, say — and every entry in the trail would
 *     then carry an identity nobody has, which is worse than carrying none. It would also attribute
 *     every visitor's changes to the same invented person, which is the one thing an audit trail
 *     must not do;
 *   • **the dashboard asks who you are** and sends that. `ActingAs.vue` in the sidebar is the whole
 *     of it, `useApi` attaches the answer to every call, and acting as nobody is both the starting
 *     state and a state the UI handles: catalogue writes are then recorded with no actor — honestly
 *     — and the Audit page explains the `401` it gets instead of pretending the trail is empty.
 *
 * **Nothing is added, removed or relaxed here.** `proxyRequest` forwards the request's headers as
 * they arrived, minus the hop-by-hop ones h3 strips (`connection`, `host`, `accept-encoding` and
 * friends), so `X-Sample-Actor` and `Accept-Language` reach the API exactly as the browser sent
 * them — which is also how the reading language has always travelled. A proxy that rewrote either
 * would be a second opinion about who is calling, in the one place nobody would think to look for
 * one.
 *
 * That the header is a poor authenticator is the API's business and the API says so at length, in
 * `src/DotNetBoostSample.Api/Authentication/SampleActorAuthentication.cs`. What matters on this
 * side is that the dashboard never claims to be somebody on a person's behalf.
 */
export default defineEventHandler(async (event) => {
  const { apiUrl } = useRuntimeConfig(event)
  const path = getRouterParam(event, 'path') ?? ''
  const { search } = getRequestURL(event)

  const target = `${apiUrl.replace(/\/$/, '')}/api/${path}${search}`

  return proxyRequest(event, target)
})
