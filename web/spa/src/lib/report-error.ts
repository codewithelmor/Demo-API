import { ApiError } from '@/lib/problem-details'

// One place to send errors to monitoring (for example Sentry or Application Insights).
// Only the status, the problem slug and the trace ID are logged: never the error object, which can hold
// request data, server text or personal data.
export function reportError(error: unknown) {
  if (error instanceof ApiError) {
    console.error('API error', {
      status: error.status,
      slug: error.slug,
      traceId: error.problem.traceId,
    })
  } else {
    console.error('Unexpected error', error instanceof Error ? error.name : typeof error)
  }
}
