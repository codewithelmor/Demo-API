/**
 * Helpers for calling the BFF's own endpoints (`/bff/login`, `/bff/user`, `/bff/logout`).
 *
 * BFF = "backend for frontend": the ASP.NET Core server that hosts this app. It keeps the sign-in session
 * in an HttpOnly cookie (JavaScript cannot read it) and forwards API calls with the real access token,
 * so the browser never sees a token.
 */
import type { z } from 'zod'
import { withDefaults } from '@/api/client'
import { ApiError, apiErrorFrom, networkError } from '@/lib/problem-details'

// The BFF's own endpoints (/bff/login, /bff/user, /bff/logout) are not in the API's OpenAPI document,
// so they are called with plain fetch and the response is checked with a Zod schema.

/**
 * Sends one request to the BFF and returns the raw response when it succeeded (status 2xx).
 * On a non-2xx answer it throws an `ApiError` built from the error body; when no answer arrives at all
 * (offline, timeout) it throws a network `ApiError`.
 *
 * @param path Address on this site, for example `/bff/user`.
 * @param init Optional HTTP method (default GET) and a body, which is sent as JSON.
 */
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

/**
 * Calls a BFF endpoint and checks that the JSON answer has the shape we expect.
 *
 * @param path   Address on this site, for example `/bff/user`.
 * @param schema Zod schema describing the expected answer. Anything that does not match is rejected.
 * @param init   Optional HTTP method and JSON body.
 * @returns The parsed, type-safe answer.
 * @throws ApiError when the server answers with an error, the network fails, or the answer has the wrong shape.
 */
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

/**
 * Calls a BFF endpoint when the success answer has no body worth reading (for example logout).
 * Throws an `ApiError` on any failure, like {@link bffJson}.
 */
export async function bffVoid(
  path: string,
  init: { method: string; body?: unknown },
): Promise<void> {
  await send(path, init)
}
