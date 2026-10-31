/** A route guard: decides whether the current user may see the pages nested inside it. */
import type { ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { Navigate, Outlet, useLocation } from 'react-router'
import { ForbiddenPage } from '@/app/forbidden-page'
import { hasRole, sessionEndKey, useSession } from '@/lib/session'

/** Props for {@link RequireRole}. */
interface RequireRoleProps {
  /** Roles that may see the children (the user needs at least one). Omit for "any signed-in user". */
  roles?: readonly string[]
  /** What to show when allowed. When omitted, the matched child route renders through `<Outlet />`. */
  children?: ReactNode
}

/**
 * Shows its children only to a signed-in user who has one of the allowed roles.
 *
 * - No session: redirects to `/sign-in`, remembering the current address as `returnUrl` so the user
 *   comes back after signing in. If the session just ended (for example it timed out), the reason is
 *   added so the sign-in page can explain it.
 * - Wrong role: shows the "not allowed" page in place, so the URL stays and the guarded page never renders.
 *
 * This is a convenience for the user, not security: the real access checks happen in the BFF and the API.
 */
export function RequireRole({ roles, children }: RequireRoleProps) {
  const user = useSession()
  const location = useLocation()
  const queryClient = useQueryClient()

  if (!user) {
    // Why the session ended, when it did so in this tab: an involuntary end (401) says so on the sign-in page and
    // keeps the way back; a sign out goes to the same address useEndSession navigates to.
    const reason = queryClient.getQueryData<string>(sessionEndKey)
    const returnUrl = encodeURIComponent(location.pathname + location.search)
    let target = `/sign-in?returnUrl=${returnUrl}`
    if (reason === 'session-ended') target = `/sign-in?reason=session-ended&returnUrl=${returnUrl}`
    else if (reason !== undefined) target = `/sign-in?reason=${reason}`
    return <Navigate to={target} replace />
  }
  if (roles && !hasRole(user, roles)) {
    return <ForbiddenPage />
  }
  return children ?? <Outlet />
}
