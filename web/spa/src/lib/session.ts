import { queryOptions, useQuery, type QueryClient } from '@tanstack/react-query'
import { z } from '@/lib/zod'
import { bffJson, bffVoid } from '@/api/bff'
import { ApiError } from '@/lib/problem-details'

// The user summary returned by POST /bff/login and GET /bff/user (ADR-0015). Never contains tokens.
// idleSecondsLeft is only sent for Admins (UX-D4); the name matches UserView in the BFF.
export const userSchema = z.object({
  email: z.string(),
  emailConfirmed: z.boolean(),
  roles: z.array(z.string()),
  customerId: z.string().nullable(),
  idleSecondsLeft: z.number().nullish(),
})

export type SessionUser = z.infer<typeof userSchema>

export const sessionKey = ['session'] as const
// Why the session ended in this tab (read by RequireRole to build the sign-in redirect). Memory only.
export const sessionEndKey = ['session-end'] as const

export type SessionEndReason =
  'session-ended' | 'signed-out' | 'password-changed' | 'account-erased'

// Drops every cached datum of the user (NFR-F33), keeping only the session query itself. The session
// query stays, so the components that watch it (the header, the route guards) are told when it changes;
// a cleared cache would leave them holding a removed query and the screen would not update.
export function dropUserData(client: QueryClient): void {
  client.getMutationCache().clear()
  client.removeQueries({
    predicate: (query) =>
      query.queryKey[0] !== sessionKey[0] && query.queryKey[0] !== sessionEndKey[0],
  })
}

// The one way the SPA leaves the signed-in state. Used by the 401 handler and by sign out, sign out
// everywhere, delete account and change password. First the session is marked as gone and the reason
// is kept, so RequireRole unmounts the pages and redirects; only then (next task, after React rendered
// that) the user's cached data is dropped. Dropping it earlier makes a still-mounted page re-create its
// query and fetch again with a dead session (a refetch storm of 401s).
export function endSessionLocally(client: QueryClient, reason: SessionEndReason): void {
  client.setQueryData(sessionEndKey, reason)
  client.setQueryData(sessionKey, null)
  setTimeout(() => {
    dropUserData(client)
  }, 0)
}

// A new session in this tab: nothing cached for a previous user may show for this one (NFR-F33).
export function startSession(client: QueryClient, user: SessionUser): void {
  dropUserData(client)
  client.removeQueries({ queryKey: sessionEndKey })
  client.setQueryData(sessionKey, user)
}

// Session bootstrap: 401 means "not signed in", which is an answer, not an error.
async function fetchSession(): Promise<SessionUser | null> {
  try {
    return await bffJson('/bff/user', userSchema)
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return null
    throw error
  }
}

export const sessionQuery = queryOptions({
  queryKey: sessionKey,
  queryFn: fetchSession,
  staleTime: 5 * 60_000,
  retry: false,
})

// Returns the cached session when there is one (even null = signed out); asks the BFF only the first time.
export function loadSession(queryClient: QueryClient): Promise<SessionUser | null> {
  return queryClient.query({ ...sessionQuery, staleTime: 'static' })
}

export function useSession(): SessionUser | null | undefined {
  return useQuery(sessionQuery).data
}

export function signIn(credentials: { email: string; password: string }): Promise<SessionUser> {
  return bffJson('/bff/login', userSchema, { method: 'POST', body: credentials })
}

export function signOut(): Promise<void> {
  return bffVoid('/bff/logout', { method: 'POST' })
}

export function hasRole(user: SessionUser, roles: readonly string[]): boolean {
  return user.roles.some((role) => roles.includes(role))
}

// Where a user lands after sign-in (information architecture: Customer /products, Admin /admin).
// The page to open after sign-in: the safe return path when this role may open it, otherwise the role's home.
// /admin needs the Admin role; everything else a signed-in user may open is shared (Customer pages are guarded by role in the router).
export function returnTargetFor(user: SessionUser, returnUrl: string | undefined): string {
  if (returnUrl === undefined) return homeFor(user)
  const isAdminPath = /^\/admin([/?#]|$)/i.test(returnUrl)
  const isAdmin = user.roles.includes('Admin')
  // An Admin has no customer pages and a Customer has no admin pages.
  if (isAdmin) return isAdminPath ? returnUrl : homeFor(user)
  return isAdminPath ? homeFor(user) : returnUrl
}

export function homeFor(user: SessionUser): string {
  return user.roles.includes('Admin') ? '/admin' : '/products'
}
