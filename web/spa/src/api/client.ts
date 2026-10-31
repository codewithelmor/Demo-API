import createClient, { type Middleware } from 'openapi-fetch'
import type { paths } from '@/api/schema'
import { apiErrorFrom, networkError } from '@/lib/problem-details'

export const REQUEST_TIMEOUT_MS = 15_000

const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS'])

// Shared by the typed client and the BFF helper: the X-CSRF header on unsafe methods (NFR-F11)
// and a 15 second timeout (NFR-F21). No token is ever added here; the BFF cookie does the work.
export function withDefaults(request: Request): Request {
  if (!SAFE_METHODS.has(request.method)) {
    request.headers.set('X-CSRF', '1')
  }
  return new Request(request, {
    signal: AbortSignal.any([request.signal, AbortSignal.timeout(REQUEST_TIMEOUT_MS)]),
  })
}

const defaults: Middleware = {
  onRequest({ request }) {
    return withDefaults(request)
  },
}

// Typed client generated from the committed OpenAPI document (npm run api:types).
// Same origin: no CORS, and the HttpOnly session cookie is sent automatically.
export const api = createClient<paths>({
  baseUrl: window.location.origin,
  credentials: 'same-origin',
  // Look up fetch per call, so test tools that patch fetch (MSW) are always used.
  fetch: (request) => globalThis.fetch(request),
})
api.use(defaults)

type CallResult<T> =
  | { data: T; error?: undefined; response: Response }
  | { data?: undefined; error: unknown; response: Response }

// For endpoints whose success has no body to use (202 "check your email", 204): resolves on any 2xx,
// throws an ApiError built from the ProblemDetails body otherwise.
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

// Like unwrap, and keeps the response too (for the ETag header).
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

// Turns an openapi-fetch result into data, or throws an ApiError built from the ProblemDetails body.
export async function unwrap<T>(call: Promise<CallResult<T>>): Promise<T> {
  return (await unwrapWithResponse(call)).data
}
