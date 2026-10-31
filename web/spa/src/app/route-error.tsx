/** The error screen React Router shows when a page or its data loader fails. */
import { useEffect } from 'react'
import { isRouteErrorResponse, Link, useRevalidator, useRouteError } from 'react-router'
import { ForbiddenPage } from '@/app/forbidden-page'
import { NotFoundPage } from '@/app/not-found-page'
import { Button, buttonVariants } from '@/components/ui/button'
import { ApiError } from '@/lib/problem-details'
import { messageFor, referenceFor } from '@/lib/problem-messages'
import { reportError } from '@/lib/report-error'

/** Pulls an HTTP status out of whatever was thrown. Returns 0 when it is not an HTTP-style error. */
function statusOf(error: unknown): number {
  if (error instanceof ApiError) return error.status
  if (isRouteErrorResponse(error)) return error.status
  return 0
}

/**
 * Route-level error boundary (SY-1, SY-2, SY-3). An "error boundary" is a component React shows
 * instead of the page when something inside it throws.
 *
 * 403 and 404 get their own pages; anything else shows a friendly message and the `traceId` as a
 * support reference the user can copy. "Try again" re-runs the route loaders.
 * The error is also passed to `reportError` once, for logging.
 */
export function RouteError() {
  const error = useRouteError()
  const revalidator = useRevalidator()

  useEffect(() => {
    reportError(error)
  }, [error])

  const status = statusOf(error)
  if (status === 403) return <ForbiddenPage />
  if (status === 404) return <NotFoundPage />

  const reference = error instanceof ApiError ? referenceFor(error) : undefined
  const message = error instanceof ApiError ? messageFor(error) : 'Something went wrong. Try again.'

  return (
    <section className="grid max-w-prose gap-4">
      <title>Something went wrong · Orders Shop</title>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        Something went wrong
      </h1>
      <p role="alert">{message}</p>
      {reference && (
        <p className="flex flex-wrap items-center gap-2 text-sm">
          If it keeps happening, contact support with this reference:{' '}
          <code className="rounded bg-muted px-1.5 py-0.5">{reference}</code>
          <Button
            variant="outline"
            size="sm"
            onClick={() => {
              void navigator.clipboard.writeText(reference)
            }}
          >
            Copy
          </Button>
        </p>
      )}
      <div className="flex gap-2">
        <Button
          onClick={() => {
            void revalidator.revalidate()
          }}
        >
          Try again
        </Button>
        <Link to="/" className={buttonVariants({ variant: 'outline' })}>
          Go to home
        </Link>
      </div>
    </section>
  )
}
