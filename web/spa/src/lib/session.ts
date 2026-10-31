import { queryOptions, useQuery, type QueryClient } from '@tanstack/react-query'
import { z } from '@/lib/zod'
import { bffJson, bffVoid } from '@/api/bff'
import { ApiError } from '@/lib/problem-details'

/**
 * Everything about "who is signed in": the session cache entry, the helpers that start and end a
 * session in this tab, and role checks.
 *
 * The session is kept by the BFF in an HttpOnly cookie. The browser only knows a small user summary,
 * which is cached under {@link sessionKey}: a user object when signed in, `null` when signed out.
 */

/**
 * Shape of the user summary returned by POST /bff/login and GET /bff/user (ADR-0015). Never contains tokens.
 * `idleSecondsLeft` is only sent for Admins (UX-D4); the name matches `UserView` in the BFF.
 */
export const userSchema = z.object({
  email: z.string(),
  emailConfirmed: z.boolean(),
  roles: z.array(z.string()),
  customerId: z.string().nullable(),
  idleSecondsLeft: z.number().nullish(),
})

/** The signed-in user as the browser knows them. */
export type SessionUser = z.infer<typeof userSchema>

/** Cache key of the session query. */
export const sessionKey = ['session'] as const
/** Cache key that remembers why the session ended in this tab (read by RequireRole to build the sign-in redirect). Memory only. */
export const sessionEndKey = ['session-end'] as const

/** Why a session ended; shown on the sign-in page as a message. */
export type SessionEndReason =
  'session-ended' | 'signed-out' | 'password-changed' | 'account-erased'

/**
 * Drops every cached datum of the user (NFR-F33), keeping only the session query itself. The session
 * query stays, so the components that watch it (the header, the route guards) are told when it changes;
 * a cleared cache would leave them holding a removed query and the screen would not update.
 */
export function dropUserData(client: QueryClient): void {
  client.getMutationCache().clear()
  client.removeQueries({
    predicate: (query) =>
      query.queryKey[0] !== sessionKey[0] && query.queryKey[0] !== sessionEndKey[0],
  })
}

/**
 * The one way the SPA leaves the signed-in state. Used by the 401 handler and by sign out, sign out
 * everywhere, delete account and change password. First the session is marked as gone and the reason
 * is kept, so RequireRole unmounts the pages and redirects; only then (next task, after React rendered
 * that) the user's cached data is dropped. Dropping it earlier makes a still-mounted page re-create its
 * query and fetch again with a dead session (a refetch storm of 401s).
 *
 * This only changes the browser side. The server-side session is ended by calling {@link signOut}.
 */
export function endSessionLocally(client: QueryClient, reason: SessionEndReason): void {
  client.setQueryData(sessionEndKey, reason)
  client.setQueryData(sessionKey, null)
  setTimeout(() => {
    dropUserData(client)
  }, 0)
}

/** Starts a session in this tab after sign-in: nothing cached for a previous user may show for this one (NFR-F33). */
export function startSession(client: QueryClient, user: SessionUser): void {
  dropUserData(client)
  client.removeQueries({ queryKey: sessionEndKey })
  client.setQueryData(sessionKey, user)
}

/** Asks the BFF who is signed in. A 401 means "not signed in", which is an answer (`null`), not an error. */
async function fetchSession(): Promise<SessionUser | null> {
  try {
    return await bffJson('/bff/user', userSchema)
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return null
    throw error
  }
}

/** Query definition (key + fetch function + cache rules) for the session. Cached for 5 minutes, never retried. */
export const sessionQuery = queryOptions({
  queryKey: sessionKey,
  queryFn: fetchSession,
  staleTime: 5 * 60_000,
  retry: false,
})

/**
 * Returns the cached session when there is one (even `null` = signed out); asks the BFF only the first time.
 * Used by route loaders, which run outside React components and so cannot use hooks.
 */
export function loadSession(queryClient: QueryClient): Promise<SessionUser | null> {
  return queryClient.query({ ...sessionQuery, staleTime: 'static' })
}

/**
 * React hook: the signed-in user, `null` when signed out, or `undefined` while it is still loading.
 * Re-renders the component when the session changes.
 */
export function useSession(): SessionUser | null | undefined {
  return useQuery(sessionQuery).data
}

/** Signs in with the BFF (POST /bff/login). The BFF sets the session cookie and returns the user summary. */
export function signIn(credentials: { email: string; password: string }): Promise<SessionUser> {
  return bffJson('/bff/login', userSchema, { method: 'POST', body: credentials })
}

/** Ends the session on the server (POST /bff/logout). Pair it with {@link endSessionLocally} for the browser side. */
export function signOut(): Promise<void> {
  return bffVoid('/bff/logout', { method: 'POST' })
}

/** True when the user has at least one of the given roles (for example `'Admin'` or `'Customer'`). */
export function hasRole(user: SessionUser, roles: readonly string[]): boolean {
  return user.roles.some((role) => roles.includes(role))
}

/**
 * The page to open after sign-in: the safe return path when this role may open it, otherwise the role's home.
 * `/admin` needs the Admin role; everything else a signed-in user may open is shared (Customer pages are
 * guarded by role in the router).
 *
 * @param returnUrl A path already checked by `safeReturnUrl`, or `undefined` when there is none.
 */
export function returnTargetFor(user: SessionUser, returnUrl: string | undefined): string {
  if (returnUrl === undefined) return homeFor(user)
  const isAdminPath = /^\/admin([/?#]|$)/i.test(returnUrl)
  const isAdmin = user.roles.includes('Admin')
  // An Admin has no customer pages and a Customer has no admin pages.
  if (isAdmin) return isAdminPath ? returnUrl : homeFor(user)
  return isAdminPath ? homeFor(user) : returnUrl
}

/** The landing page of a role (information architecture): Customer `/products`, Admin `/admin`. */
export function homeFor(user: SessionUser): string {
  return user.roles.includes('Admin') ? '/admin' : '/products'
}
