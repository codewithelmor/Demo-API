import { z } from '@/lib/zod'

// RFC 9457 problem details, as returned by the BFF and the API (endpoint catalog section 1.8).
// Parsed with Zod, so anything else the server sends can never be mistaken for a problem.
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

export type ProblemDetails = z.infer<typeof problemSchema>

const PROBLEM_PREFIX = '/problems/'

// The stable error code clients switch on: "/problems/invalid-credentials" -> "invalid-credentials".
export function slugOf(problem: ProblemDetails): string | undefined {
  const type = problem.type
  if (!type) return undefined
  const index = type.lastIndexOf(PROBLEM_PREFIX)
  return index === -1 ? undefined : type.slice(index + PROBLEM_PREFIX.length)
}

export class ApiError extends Error {
  readonly problem: ProblemDetails
  readonly slug: string | undefined
  // From the Retry-After header (seconds), when the server sent one.
  readonly retryAfterSeconds: number | undefined

  constructor(problem: ProblemDetails, retryAfterSeconds?: number) {
    super(problem.title ?? 'The request failed.')
    this.name = 'ApiError'
    this.problem = problem
    this.slug = slugOf(problem)
    this.retryAfterSeconds = retryAfterSeconds
  }

  get status(): number {
    return this.problem.status ?? 0
  }
}

function parseRetryAfter(response: Response): number | undefined {
  const value = response.headers.get('Retry-After')
  if (value === null) return undefined
  const seconds = Number(value)
  return Number.isFinite(seconds) && seconds >= 0 ? Math.ceil(seconds) : undefined
}

// Builds an ApiError from a response body that was already read (any shape) and the response.
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

// Used when no response arrived at all: offline, DNS, timeout (NFR-F21).
export function networkError(cause: unknown): ApiError {
  const timedOut = cause instanceof DOMException && cause.name === 'TimeoutError'
  return new ApiError({
    type: timedOut ? '/problems/timeout' : '/problems/network-error',
    title: timedOut ? 'The request timed out.' : 'The network request failed.',
    status: 0,
  })
}

// First message per field, with keys in camelCase so they match form field names.
// ASP.NET Core can send "Email" or "email" depending on JSON settings.
export function fieldErrors(problem: ProblemDetails): Record<string, string> {
  const result: Record<string, string> = {}
  for (const [key, messages] of Object.entries(problem.errors ?? {})) {
    const first = messages[0]
    if (first === undefined || key === '') continue
    result[key.charAt(0).toLowerCase() + key.slice(1)] = first
  }
  return result
}
