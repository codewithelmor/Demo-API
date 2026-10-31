import { ApiError } from '@/lib/problem-details'
import { messageFor, referenceFor } from '@/lib/problem-messages'

/**
 * One line of text for an error alert: the friendly message for the error, plus the reference
 * (the server's `traceId`) when it helps support find the request. Non-API errors get a generic message.
 */
export function apiErrorMessage(error: unknown): string {
  if (!(error instanceof ApiError)) return 'Something went wrong. Try again.'
  const reference = referenceFor(error)
  const message = messageFor(error)
  return reference ? `${message} Reference: ${reference}` : message
}
