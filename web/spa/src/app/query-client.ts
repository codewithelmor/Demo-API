/**
 * Creates the TanStack Query `QueryClient`: the in-memory cache and fetching engine for all server data.
 * Pages ask it for data ("queries") or send changes ("mutations") and it handles caching, retries and
 * loading/error state. This file holds the app-wide rules for that.
 */
import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query'
import { endSessionLocally } from '@/lib/session'
import { ApiError } from '@/lib/problem-details'

/**
 * Builds the one `QueryClient` for the app (tests build their own so they do not share state).
 *
 * Defaults set here:
 * - Data counts as fresh for 30 seconds, so going back to a page does not refetch at once.
 * - A failed read is retried once, but only for network/server errors (not 4xx), because a 4xx answer
 *   will not change by asking again. Writes (mutations) never retry, to avoid doing something twice.
 * - Any 401 (other than a wrong password) ends the session in this tab.
 */
export function createQueryClient(): QueryClient {
  // A 401 from any call other than sign-in means the BFF session ended (idle, refresh failed, locked
  // account). Everything cached for that user is dropped and the guarded routes redirect to sign-in with
  // "session ended". Deferred a tick, so the failing query finishes updating first.
  const onError = (error: Error) => {
    if (error instanceof ApiError && error.status === 401 && error.slug !== 'invalid-credentials') {
      queueMicrotask(() => {
        endSessionLocally(client, 'session-ended')
      })
    }
  }

  const client: QueryClient = new QueryClient({
    queryCache: new QueryCache({ onError }),
    mutationCache: new MutationCache({ onError }),
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        // Retry network and server errors once; never 4xx (they will not change). Writes never retry.
        retry: (failureCount, error) =>
          !(error instanceof ApiError && error.status >= 400 && error.status < 500) &&
          failureCount < 1,
      },
      mutations: { retry: false },
    },
  })
  return client
}
