import type { z } from 'zod'
import { withDefaults } from '@/api/client'
import { ApiError, apiErrorFrom, networkError } from '@/lib/problem-details'

// The BFF's own endpoints (/bff/login, /bff/user, /bff/logout) are not in the API's OpenAPI document,
// so they are called with plain fetch and the response is checked with a Zod schema.

async function send(path: string, init?: { method: string; body?: unknown }): Promise<Response> {
  const headers = new Headers({ Accept: 'application/json, application/problem+json' })
  if (init?.body !== undefined) headers.set('Content-Type', 'application/json')
  const request = withDefaults(
    new Request(new URL(path, window.location.origin), {
      method: init?.method ?? 'GET',
      headers,
      credentials: 'same-origin',
      body: init?.body === undefined ? null : JSON.stringify(init.body),
    }),
  )
  let response: Response
  try {
    response = await globalThis.fetch(request)
  } catch (cause) {
    throw networkError(cause)
  }
  if (!response.ok) {
    const body: unknown = await response.json().catch(() => undefined)
    throw apiErrorFrom(body, response)
  }
  return response
}

export async function bffJson<T>(
  path: string,
  schema: z.ZodType<T>,
  init?: { method: string; body?: unknown },
): Promise<T> {
  const response = await send(path, init)
  try {
    return schema.parse(await response.json())
  } catch {
    // A 2xx with a body we do not recognise is an error the user can retry, not a crash (never shows the body).
    throw new ApiError({
      type: '/problems/invalid-response',
      title: 'The service sent an unexpected answer.',
      status: 0,
    })
  }
}

export async function bffVoid(
  path: string,
  init: { method: string; body?: unknown },
): Promise<void> {
  await send(path, init)
}
