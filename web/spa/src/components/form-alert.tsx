import type { ServerFailure } from '@/hooks/use-server-failure'
import { messageFor, referenceFor } from '@/lib/problem-messages'

/** Props for {@link FormAlert}. */
interface FormAlertProps {
  /** The last server failure from `useServerFailure`, or `undefined` when there is none (nothing renders). */
  failure: ServerFailure | undefined
}

/**
 * The form-level error box: a live region (`role="alert"`) so screen readers announce it (NFR-F03).
 * It shows the friendly message, any server messages that belong to no field, and a support reference.
 * For a 429 the text is the server's Retry-After at the time of the response; the visible countdown
 * is next to the submit button (SubmitRow), so this region is not re-announced every second.
 */
export function FormAlert({ failure }: FormAlertProps) {
  if (!failure) return null
  const reference = referenceFor(failure.error)
  return (
    <div role="alert" className="rounded-md border border-destructive p-3 text-sm">
      <p>{messageFor(failure.error)}</p>
      {failure.unmapped.map((message) => (
        <p key={message} className="mt-1">
          {message}
        </p>
      ))}
      {reference && <p className="mt-1 text-muted-foreground">Reference: {reference}</p>}
    </div>
  )
}
