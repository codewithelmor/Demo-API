import { ApiError } from '@/lib/problem-details'
import { messageFor, referenceFor } from '@/lib/problem-messages'

// One line for an error Alert: the mapped message, plus the reference (traceId) when it helps support.
export function apiErrorMessage(error: unknown): string {
  if (!(error instanceof ApiError)) return 'Something went wrong. Try again.'
  const reference = referenceFor(error)
  const message = messageFor(error)
  return reference ? `${message} Reference: ${reference}` : message
}
