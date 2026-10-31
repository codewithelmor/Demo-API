/**
 * The typed HTTP client for the Orders API.
 *
 * `api` is generated from the API's OpenAPI document (`npm run api:types` writes `schema.d.ts`), so route
 * names, parameters and response types are checked by the compiler. The helpers below turn its
 * `{ data, error, response }` results into plain values, or into a thrown `ApiError`.
 */
import createClient, { type Middleware } from 'openapi-fetch'
import type { paths } from '@/api/schema'
import { apiErrorFrom, networkError } from '@/lib/problem-details'

/** How long a request may take before it is cancelled (NFR-F21). */
export const REQUEST_TIMEOUT_MS = 15_000

/** HTTP methods that only read data. Everything else changes data and needs the CSRF header. */
const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS'])

/**
 * Adds the settings every request needs. Shared by the typed client and the BFF helper.
 *
 * - `X-CSRF: 1` on methods that change data (NFR-F11). A malicious site cannot add custom headers to a
 *   cross-site request, so the BFF can reject requests without it (CSRF protection).
 * - A 15 second timeout (NFR-F21), combined with any cancellation signal the caller already set.
 *
 * No token is ever added here; the BFF session cookie does the work.
 */
export function withDefaults(request: Request): Request {
  if (!SAFE_METHODS.has(request.method)) {
    request.headers.set('X-CSRF', '1')
  }
  return new Request(request, {
    signal: AbortSignal.any([request.signal, AbortSignal.timeout(REQUEST_TIMEOUT_MS)]),
  })
}

/** openapi-fetch "middleware": a hook that runs on every request the typed client sends. */
const defaults: Middleware = {
  onRequest({ request }) {
    return withDefaults(request)
  },
}

/**
 * Typed client generated from the committed OpenAPI document (npm run api:types).
 * Same origin (the BFF serves both the app and the API proxy): no CORS, and the HttpOnly session cookie
 * is sent automatically. Example: `api.GET('/api/v1/products/{productId}', { params: { path: { productId } } })`.
 */
export const api = createClient<paths>({
  baseUrl: window.location.origin,
  credentials: 'same-origin',
  // Look up fetch per call, so test tools that patch fetch (MSW) are always used.
  fetch: (request) => globalThis.fetch(request),
})
api.use(defaults)

/** What openapi-fetch returns: either `data` (success) or `error` (a non-2xx answer), plus the raw response. */
type CallResult<T> =
  | { data: T; error?: undefined; response: Response }
  | { data?: undefined; error: unknown; response: Response }

/**
 * For endpoints whose success has no body to use (202 "check your email", 204): resolves on any 2xx,
 * throws an `ApiError` built from the ProblemDetails body otherwise.
 */
export async function expectOk(
  call: Promise<{ error?: unknown; response: Response }>,
): Promise<void> {
  let result: { error?: unknown; response: Response }
  try {
    result = await call
  } catch (cause) {
    throw networkError(cause)
  }
  if (!result.response.ok) throw apiErrorFrom(result.error, result.response)
}

/**
 * Like {@link unwrap}, and keeps the response too (for the ETag header, a version tag the server sends
 * so a later update can prove it edited the latest version).
 */
export async function unwrapWithResponse<T>(
  call: Promise<CallResult<T>>,
): Promise<{ data: T; response: Response }> {
  let result: CallResult<T>
  try {
    result = await call
  } catch (cause) {
    throw networkError(cause)
  }
  if (result.data !== undefined) return { data: result.data, response: result.response }
  throw apiErrorFrom(result.error, result.response)
}

/**
 * Turns an openapi-fetch result into data, or throws an `ApiError` built from the ProblemDetails body.
 * Use it to wrap every typed `api.GET/POST/...` call: `unwrap(api.GET(...))`.
 */
export async function unwrap<T>(call: Promise<CallResult<T>>): Promise<T> {
  return (await unwrapWithResponse(call)).data
}
