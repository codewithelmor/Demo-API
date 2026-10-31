import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query'
import { endSessionLocally } from '@/lib/session'
import { ApiError } from '@/lib/problem-details'

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
