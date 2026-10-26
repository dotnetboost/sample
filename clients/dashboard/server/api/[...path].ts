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
 * Nobody the dashboard chooses. The API's stand-in scheme signs every request in as one sample
 * user — see `src/DotNetBoostSample.Api/Authentication/SampleActorAuthentication.cs` — so nothing
 * here adds an identity header. `proxyRequest` forwards the request's headers as they arrived,
 * minus the hop-by-hop ones h3 strips, which is how `Accept-Language` reaches the API.
 */
export default defineEventHandler(async (event) => {
  const { apiUrl } = useRuntimeConfig(event)
  const path = getRouterParam(event, 'path') ?? ''
  const { search } = getRequestURL(event)

  const target = `${apiUrl.replace(/\/$/, '')}/api/${path}${search}`

  return proxyRequest(event, target)
})
