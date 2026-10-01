/**
 * Proxies every `/api/**` call through to the sample API — the three libraries' endpoints and the
 * application's own alike, which is why all of them live under `/api` on the server.
 *
 * The browser therefore only ever talks to this app's own origin, and the target moves between
 * environments by changing `NUXT_API_URL` alone. An upload streams through Nitro rather than
 * being buffered here: `proxyRequest` forwards the request body as it arrives.
 */
export default defineEventHandler(async (event) => {
  const { apiUrl } = useRuntimeConfig(event)
  const path = getRouterParam(event, 'path') ?? ''
  const { search } = getRequestURL(event)

  const target = `${apiUrl.replace(/\/$/, '')}/api/${path}${search}`

  return proxyRequest(event, target)
})
