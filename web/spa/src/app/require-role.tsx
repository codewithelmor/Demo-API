import type { ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { Navigate, Outlet, useLocation } from 'react-router'
import { ForbiddenPage } from '@/app/forbidden-page'
import { hasRole, sessionEndKey, useSession } from '@/lib/session'

interface RequireRoleProps {
  // Roles that may see the children. Omit for "any signed-in user".
  roles?: readonly string[]
  children?: ReactNode
}

// UX guard only (the API and BFF enforce access). No session: go to sign-in and come back after.
// Wrong role: show "not allowed" in place, so the URL stays and the guarded page never renders.
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
