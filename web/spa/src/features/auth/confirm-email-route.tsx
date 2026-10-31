import { Link, useLoaderData, useRevalidator, type LoaderFunctionArgs } from 'react-router'
import { TextLink } from '@/components/text-link'
import { Button, buttonVariants } from '@/components/ui/button'
import { confirmEmail } from '@/features/auth/access-api'
import { ResendConfirmationForm } from '@/features/auth/resend-confirmation-form'
import { useCountdown } from '@/hooks/use-countdown'
import { ApiError } from '@/lib/problem-details'
import { messageFor, referenceFor } from '@/lib/problem-messages'

/** What the confirm-email loader found out; the page shows a different screen for each. */
export type ConfirmOutcome =
  | { kind: 'confirmed' }
  | { kind: 'invalid' }
  // 429, network and server errors: the link may still be good, so the user can try again.
  | { kind: 'failed'; error: ApiError }

/** Format of a user ID in the link (a GUID). */
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

/**
 * Route loader for the link in the confirmation email. It runs once per visit, not once per render,
 * so React StrictMode cannot post twice (AN-5).
 * 1. Read userId and token from the link.
 * 2. Remove them from the address bar and the browser history entry (NFR-F43), so the token cannot leak
 *    through history, screenshots or the Referer header.
 * 3. Only then call EP-02.
 * "Try again" after a 429 revalidates this loader; the router still holds the original location.
 */
export async function confirmEmailLoader({ request }: LoaderFunctionArgs): Promise<ConfirmOutcome> {
  const params = new URL(request.url).searchParams
  const userId = params.get('userId')
  const token = params.get('token')

  if (window.location.search !== '') {
    window.history.replaceState(window.history.state, '', window.location.pathname)
  }

  if (!userId || !GUID.test(userId) || !token) return { kind: 'invalid' }

  try {
    await confirmEmail({ userId, token }, request.signal)
    return { kind: 'confirmed' }
  } catch (error) {
    if (!(error instanceof ApiError)) throw error
    if (error.slug === 'invalid-token' || error.slug === 'validation-failed') {
      return { kind: 'invalid' }
    }
    return { kind: 'failed', error }
  }
}

/** AN-5: the page the email link opens. It shows the loader's result: confirmed, invalid link, or failed. */
export function ConfirmEmailRoute() {
  const outcome = useLoaderData<ConfirmOutcome>()
  return (
    <section className="mx-auto grid max-w-md gap-4 rounded-lg border p-6">
      <title>Confirm your email · Orders Shop</title>
      {outcome.kind === 'confirmed' && <Confirmed />}
      {outcome.kind === 'invalid' && <InvalidLink />}
      {outcome.kind === 'failed' && <Failed error={outcome.error} />}
    </section>
  )
}

/** Success screen. */
function Confirmed() {
  return (
    <>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        Email confirmed
      </h1>
      <p role="status">Email confirmed. You can now sign in.</p>
      <div>
        <Link to="/sign-in" className={buttonVariants()}>
          Sign in
        </Link>
      </div>
    </>
  )
}

/** The link is bad or expired: offers to send a new one. */
function InvalidLink() {
  return (
    <>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        This link is invalid or expired
      </h1>
      <p role="status">Enter your email and we will send a new link.</p>
      <ResendConfirmationForm submitLabel="Send new link" />
      <p>
        <TextLink to="/sign-in">Back to sign in</TextLink>
      </p>
    </>
  )
}

/** A temporary failure (network, server, 429): the link may still be good, so "Try again" re-runs the loader. */
function Failed({ error }: { error: ApiError }) {
  const revalidator = useRevalidator()
  const secondsLeft = useCountdown(error, error.status === 429 ? (error.retryAfterSeconds ?? 0) : 0)
  const reference = referenceFor(error)
  return (
    <>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        We could not confirm your email
      </h1>
      <div role="alert" className="rounded-md border border-destructive p-3 text-sm">
        <p>{messageFor(error)}</p>
        {reference && <p className="mt-1 text-muted-foreground">Reference: {reference}</p>}
      </div>
      <div className="flex flex-wrap items-center gap-3">
        <Button
          type="button"
          disabled={secondsLeft > 0 || revalidator.state === 'loading'}
          aria-busy={revalidator.state === 'loading'}
          onClick={() => {
            void revalidator.revalidate()
          }}
        >
          Try again
        </Button>
        {secondsLeft > 0 && (
          <span aria-hidden="true" className="text-sm text-muted-foreground">
            You can try again in {secondsLeft} s
          </span>
        )}
      </div>
    </>
  )
}
