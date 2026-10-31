/**
 * Wraps a route "loader" so it only runs for users who are allowed to see the page.
 * (A loader is a function React Router runs before showing a page, to fetch the data the page needs.)
 */
import type { QueryClient } from '@tanstack/react-query'
import type { LoaderFunctionArgs } from 'react-router'
import { hasRole, loadSession } from '@/lib/session'

/**
 * Route loaders run in parallel with the parent route's component, so `RequireRole` cannot stop them.
 * This wrapper checks the session (already cached, so no request) before the loader may call the API:
 * an anonymous visitor or the wrong role starts no request; the page itself then shows sign-in or "not allowed".
 *
 * @param queryClient The shared data cache, used to read the cached session.
 * @param roles       Roles that may load the page. `undefined` means any signed-in user.
 * @param loader      The real loader to run when the user is allowed.
 * @returns A loader that returns `null` (no data) for a user who is not allowed, else the real loader's result.
 */
export function guarded(
  queryClient: QueryClient,
  roles: readonly string[] | undefined,
  loader: (args: LoaderFunctionArgs) => unknown,
) {
  return async (args: LoaderFunctionArgs): Promise<unknown> => {
    const user = await loadSession(queryClient)
    if (!user || (roles && !hasRole(user, roles))) return null
    return loader(args)
  }
}
