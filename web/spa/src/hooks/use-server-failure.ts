import { useState } from 'react'
import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'
import { useCountdown } from '@/hooks/use-countdown'
import { ApiError, fieldErrors, networkError } from '@/lib/problem-details'

export interface ServerFailure {
  error: ApiError
  // Server messages for keys that have no field on this form (shown in the form-level alert).
  unmapped: string[]
}

// Shared by the anonymous access forms: keeps the last server failure, maps ProblemDetails `errors`
// to form fields (focus goes to the first one), and runs the 429 countdown (NFR-F21, F23).
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
