import { useMutation, useQuery, type QueryClient } from '@tanstack/react-query'
import { Link, useLocation } from 'react-router'
import { Button, buttonVariants } from '@/components/ui/button'
import { AccountLayout } from '@/features/profile/account-nav'
import { profileQuery, signOutEverywhere } from '@/features/profile/profile-api'
import { useEndSession } from '@/hooks/use-end-session'
import { formatDateTime } from '@/lib/dates'
import { apiErrorMessage } from '@/lib/error-message'
import { ApiError } from '@/lib/problem-details'

export function accountLoader(queryClient: QueryClient) {
  return () => {
    // Not awaited: the page renders its loading state while the request runs.
    queryClient.query(profileQuery).catch(() => undefined)
    return null
  }
}

function savedFlag(state: unknown): boolean {
  return typeof state === 'object' && state !== null && 'saved' in state && state.saved === true
}

// CU-7 (EP-35)
export function AccountRoute() {
  const location = useLocation()
  const { data, error, isPending, isError, refetch, isFetching } = useQuery(profileQuery)

  return (
    <AccountLayout>
      <title>My account · Orders Shop</title>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        My account
      </h1>
      {/* Always in the page, so the message is announced when it appears. */}
      <p role="status" className="min-h-5 text-sm">
        {savedFlag(location.state) ? 'Profile saved.' : ''}
      </p>

      {isPending && <p role="status">Loading your profile…</p>}

      {isError && error instanceof ApiError && error.status === 404 && (
        <p role="alert" className="rounded-md border border-destructive p-3">
          We could not find your account.
        </p>
      )}
      {isError && !(error instanceof ApiError && error.status === 404) && (
        <div
          role="alert"
          className="grid justify-items-start gap-3 rounded-md border border-destructive p-4"
        >
          <p>{apiErrorMessage(error)}</p>
          <Button
            variant="outline"
            disabled={isFetching}
            onClick={() => {
              void refetch()
            }}
          >
            {isFetching ? 'Trying again…' : 'Try again'}
          </Button>
        </div>
      )}

      {data && (
        <section aria-labelledby="profile-heading" className="grid gap-3 rounded-lg border p-4">
          <h2 id="profile-heading" className="text-lg font-medium">
            Profile
          </h2>
          <dl className="grid gap-3 text-sm sm:grid-cols-[8rem_1fr]">
            <dt className="font-medium">Name</dt>
            <dd>
              {data.profile.firstName} {data.profile.lastName}
            </dd>
            <dt className="font-medium">Email</dt>
            <dd>
              {data.profile.email}
              <span className="block text-muted-foreground">
                To change your email, contact support.
              </span>
            </dd>
            <dt className="font-medium">Phone</dt>
            <dd>{data.profile.phone ?? 'Not added'}</dd>
            <dt className="font-medium">Member since</dt>
            <dd>{formatDateTime(data.profile.createdAt)}</dd>
          </dl>
          <div>
            <Link to="/account/edit" className={buttonVariants()}>
              Edit profile
            </Link>
          </div>
        </section>
      )}

      <SignOutEverywhere />
    </AccountLayout>
  )
}

// CU-7, US-F036 (EP-07). The BFF deletes this session and the API revokes the other sessions' tokens,
// so they end on their next refresh. The cart on this device is cleared, as for any sign out.
function SignOutEverywhere() {
  const endSession = useEndSession()
  const mutation = useMutation({
    mutationFn: signOutEverywhere,
    // Like Sign out: the BFF removes the session even when the API call fails.
    onSettled: () => endSession('signed-out', { clearCart: true }),
  })

  return (
    <section aria-labelledby="everywhere-heading" className="grid gap-3 rounded-lg border p-4">
      <h2 id="everywhere-heading" className="text-lg font-medium">
        Sign out everywhere
      </h2>
      <p className="text-sm">
        Ends your session on every device. Other devices are signed out the next time they refresh.
      </p>
      <div>
        <Button
          variant="outline"
          disabled={mutation.isPending}
          aria-busy={mutation.isPending}
          onClick={() => {
            mutation.mutate()
          }}
        >
          {mutation.isPending ? 'Signing out…' : 'Sign out everywhere'}
        </Button>
      </div>
    </section>
  )
}
