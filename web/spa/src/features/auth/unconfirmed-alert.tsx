import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { resendConfirmation } from '@/features/auth/access-api'
import { useCountdown } from '@/hooks/use-countdown'
import { ApiError, networkError } from '@/lib/problem-details'
import { messageFor } from '@/lib/problem-messages'

export const RESEND_SENT = 'If an account exists for this email, we sent a new link.'

// Sign-in got 403 email-not-confirmed (AN-2). [Resend email] reuses the email already typed (WCAG 3.3.7),
// and the alert then changes to the same neutral text for every email.
export function UnconfirmedAlert({ email, message }: { email: string; message: string }) {
  const [sent, setSent] = useState(false)
  const [failure, setFailure] = useState<ApiError | undefined>()
  const rateLimited = failure?.status === 429 ? failure : undefined
  const secondsLeft = useCountdown(rateLimited, rateLimited?.retryAfterSeconds ?? 0)

  const mutation = useMutation({
    mutationFn: resendConfirmation,
    onSuccess: () => {
      setSent(true)
    },
    onError: (error) => {
      setFailure(error instanceof ApiError ? error : networkError(error))
    },
  })

  return (
    <div role="alert" className="grid justify-items-start gap-2 rounded-md border p-3 text-sm">
      {sent ? (
        // Focus moves here: the button that had it is gone.
        <p
          tabIndex={-1}
          className="outline-none"
          ref={(element) => {
            element?.focus()
          }}
        >
          {RESEND_SENT}
        </p>
      ) : (
        <>
          <p>{message}</p>
          {failure && <p>{messageFor(failure)}</p>}
          <Button
            type="button"
            size="sm"
            variant="outline"
            disabled={mutation.isPending || secondsLeft > 0}
            aria-busy={mutation.isPending}
            onClick={() => {
              setFailure(undefined)
              mutation.mutate(email)
            }}
          >
            {mutation.isPending ? 'Sending…' : 'Resend email'}
          </Button>
        </>
      )}
    </div>
  )
}
