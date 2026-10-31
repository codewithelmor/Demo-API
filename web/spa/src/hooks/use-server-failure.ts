import { useState } from 'react'
import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'
import { useCountdown } from '@/hooks/use-countdown'
import { ApiError, fieldErrors, networkError } from '@/lib/problem-details'

/** The last error the server sent for a form submit. */
export interface ServerFailure {
  /** The error itself, for the form-level alert and the 429 handling. */
  error: ApiError
  // Server messages for keys that have no field on this form (shown in the form-level alert).
  unmapped: string[]
}

/**
 * React hook shared by the anonymous access forms: keeps the last server failure, maps ProblemDetails
 * `errors` to form fields (focus goes to the first one), and runs the 429 countdown (NFR-F21, F23).
 *
 * @param setError   React Hook Form's `setError`, used to show a server message under a field.
 * @param fieldNames The form's field names; server errors for other keys go to `unmapped`.
 * @returns `failure` (last error or undefined), `secondsLeft` and `locked` (the 429 wait, during which the
 *          submit button stays disabled), `fail(cause)` to call from a catch block, and `clear()`.
 */
export function useServerFailure<V extends FieldValues>(
  setError: UseFormSetError<V>,
  fieldNames: readonly Path<V>[],
) {
  const [failure, setFailure] = useState<ServerFailure | undefined>()

  const rateLimited = failure?.error.status === 429 ? failure.error : undefined
  const secondsLeft = useCountdown(rateLimited, rateLimited?.retryAfterSeconds ?? 0)
  const locked = rateLimited !== undefined && secondsLeft > 0

  function fail(cause: unknown) {
    const error = cause instanceof ApiError ? cause : networkError(cause) // not an API problem: show the generic network message
    const unmapped: string[] = []
    let focused = false
    for (const [name, message] of Object.entries(fieldErrors(error.problem))) {
      const field = fieldNames.find((candidate) => candidate === name)
      if (field === undefined) {
        unmapped.push(message)
        continue
      }
      setError(field, { type: 'server', message }, { shouldFocus: !focused })
      focused = true
    }
    setFailure({ error, unmapped })
  }

  function clear() {
    setFailure(undefined)
  }

  return { failure, secondsLeft, locked, fail, clear }
}
