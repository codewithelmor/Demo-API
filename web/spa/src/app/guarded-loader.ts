import type { QueryClient } from '@tanstack/react-query'
import type { LoaderFunctionArgs } from 'react-router'
import { hasRole, loadSession } from '@/lib/session'

// Route loaders run in parallel with the parent route's component, so RequireRole cannot stop them.
// This wrapper checks the session (already cached, no request) before the loader may call the API:
// an anonymous visitor or the wrong role starts no request; the page itself then shows sign-in or "not allowed".
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
