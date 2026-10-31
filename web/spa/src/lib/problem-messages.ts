import { ApiError } from '@/lib/problem-details'

// Slug -> user text (wireframes section 0.2). Unknown slugs fall back to the title (NFR-F20).
const messages: Record<string, string> = {
  'validation-failed': 'Check the highlighted fields and try again.',
  'invalid-token': 'This link is invalid or expired.',
  'invalid-credentials': 'Email or password is incorrect.',
  'email-not-confirmed':
    'Confirm your email first. We sent you a link when you created your account.',
  'session-ended': 'Your session ended. Sign in again to continue.',
  unauthorized: 'Your session ended. Sign in again to continue.',
  forbidden: 'You do not have access to this.',
  'csrf-check-failed': 'Refresh the page and try again.',
  'not-found': 'We could not find that.',
  'upstream-unavailable': 'The service is not available right now. Try again in a moment.',
  'service-unavailable': 'The service is busy. Try again in a moment.',
  'insufficient-stock': 'Some products do not have enough stock.',
  'too-many-pending-orders':
    'You have too many open orders. Wait for one to be confirmed or cancel one.',
  'invalid-status-transition': 'This order can no longer be cancelled.',
  'precondition-failed': 'This record changed since you opened it.',
  'customer-has-open-orders': 'Cancel or finish your open orders first.',
  'rate-limited': 'Too many attempts. Try again in a moment.',
  'concurrency-conflict': 'Someone changed this at the same time. We reloaded it. Try again.',
  'invalid-order-state': 'This order can no longer be changed.',
  'invalid-refresh-token': 'Your session ended. Sign in again to continue.',
  'invalid-response': 'Something went wrong. Try again.',
  'internal-error': 'Something went wrong. Try again.',
  timeout: 'The request took too long. Try again.',
  'network-error': 'Could not reach the server. Check your connection and try again.',
  // Client bugs (wireframes 0.2): generic text, and the reference so support can find the request.
  'payload-too-large': 'Something went wrong on our side.',
  'unsupported-media-type': 'Something went wrong on our side.',
  'method-not-allowed': 'Something went wrong on our side.',
  'precondition-required': 'Something went wrong on our side.',
  'email-change-not-allowed': 'Something went wrong on our side.',
}

const clientBugSlugs = new Set([
  'payload-too-large',
  'unsupported-media-type',
  'method-not-allowed',
  'precondition-required',
  'email-change-not-allowed',
])

export function rateLimitMessage(seconds: number): string {
  return `Too many attempts. Try again in ${String(seconds)} seconds.`
}

export function messageFor(error: ApiError): string {
  if (error.status === 429 || error.slug === 'rate-limited') {
    return error.retryAfterSeconds === undefined
      ? 'Too many attempts. Try again in a moment.'
      : rateLimitMessage(error.retryAfterSeconds)
  }
  const known = error.slug === undefined ? undefined : messages[error.slug]
  if (known !== undefined) return known
  if (error.status >= 500) return 'Something went wrong. Try again.'
  return error.problem.title ?? 'Something went wrong.'
}

// The reference (traceId) is shown for server errors, client-bug slugs and slugs the app does not know.
export function referenceFor(error: ApiError): string | undefined {
  const unknown = error.slug === undefined || !(error.slug in messages)
  const clientBug = error.slug !== undefined && clientBugSlugs.has(error.slug)
  return error.status >= 500 || unknown || clientBug
    ? (error.problem.traceId ?? undefined)
    : undefined
}
