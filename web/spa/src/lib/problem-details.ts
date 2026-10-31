import { z } from '@/lib/zod'

/**
 * Errors from the server. RFC 9457 "problem details" is the standard JSON shape for an HTTP error:
 * `{ type, title, status, detail, traceId, errors }`. The BFF and the API both use it
 * (endpoint catalog section 1.8). It is parsed with Zod, so anything else the server sends can never be
 * mistaken for a problem.
 */
const problemSchema = z.object({
  type: z.string().nullish(),
  title: z.string().nullish(),
  status: z.number().nullish(),
  detail: z.string().nullish(),
  instance: z.string().nullish(),
  traceId: z.string().nullish(),
  errors: z.record(z.string(), z.array(z.string())).nullish(),
  // Extensions the order endpoints add (endpoint catalog 1.8).
  productIds: z.array(z.string()).nullish(), // 409 insufficient-stock
  maxPending: z.number().nullish(), // 409 too-many-pending-orders
  currentStatus: z.string().nullish(), // 409 invalid-status-transition
})

/** The parsed error body. Every field is optional because we cannot trust what the network returns. */
export type ProblemDetails = z.infer<typeof problemSchema>

/** Every error `type` the server sends looks like `.../problems/<slug>`. */
const PROBLEM_PREFIX = '/problems/'

/** The stable error code clients switch on: `"/problems/invalid-credentials"` becomes `"invalid-credentials"`. */
export function slugOf(problem: ProblemDetails): string | undefined {
  const type = problem.type
  if (!type) return undefined
  const index = type.lastIndexOf(PROBLEM_PREFIX)
  return index === -1 ? undefined : type.slice(index + PROBLEM_PREFIX.length)
}

/**
 * The one error type thrown for any failed server call (HTTP error, network failure, timeout, bad answer).
 * Catch it and read `status` (0 means no answer arrived), `slug` (the stable error code) and `problem`.
 */
export class ApiError extends Error {
  /** The full parsed error body. */
  readonly problem: ProblemDetails
  /** The short error code, for example `"invalid-credentials"`. `undefined` when the server sent none. */
  readonly slug: string | undefined
  /** From the Retry-After header (seconds), when the server sent one. Set on 429 "too many requests". */
  readonly retryAfterSeconds: number | undefined

  constructor(problem: ProblemDetails, retryAfterSeconds?: number) {
    super(problem.title ?? 'The request failed.')
    this.name = 'ApiError'
    this.problem = problem
    this.slug = slugOf(problem)
    this.retryAfterSeconds = retryAfterSeconds
  }

  /** The HTTP status code, or 0 when there was no HTTP answer (offline, timeout). */
  get status(): number {
    return this.problem.status ?? 0
  }
}

/** Reads the Retry-After header as whole seconds. Returns `undefined` when it is missing or not a number. */
function parseRetryAfter(response: Response): number | undefined {
  const value = response.headers.get('Retry-After')
  if (value === null) return undefined
  const seconds = Number(value)
  return Number.isFinite(seconds) && seconds >= 0 ? Math.ceil(seconds) : undefined
}

/** Builds an `ApiError` from a response body that was already read (any shape) and the response itself. */
export function apiErrorFrom(body: unknown, response: Response): ApiError {
  const parsed = problemSchema.safeParse(body)
  const problem: ProblemDetails = parsed.success ? parsed.data : {}
  return new ApiError(
    {
      ...problem,
      status: problem.status ?? response.status,
      title: problem.title ?? response.statusText,
    },
    parseRetryAfter(response),
  )
}

/** Builds an `ApiError` for when no response arrived at all: offline, DNS failure, timeout (NFR-F21). */
export function networkError(cause: unknown): ApiError {
  const timedOut = cause instanceof DOMException && cause.name === 'TimeoutError'
  return new ApiError({
    type: timedOut ? '/problems/timeout' : '/problems/network-error',
    title: timedOut ? 'The request timed out.' : 'The network request failed.',
    status: 0,
  })
}

/**
 * Picks the server's validation messages for form fields: the first message per field, with keys in
 * camelCase so they match form field names. ASP.NET Core can send "Email" or "email" depending on JSON settings.
 *
 * @returns For example `{ email: 'Enter a valid email.' }`.
 */
export function fieldErrors(problem: ProblemDetails): Record<string, string> {
  const result: Record<string, string> = {}
  for (const [key, messages] of Object.entries(problem.errors ?? {})) {
    const first = messages[0]
    if (first === undefined || key === '') continue
    result[key.charAt(0).toLowerCase() + key.slice(1)] = first
  }
  return result
}
