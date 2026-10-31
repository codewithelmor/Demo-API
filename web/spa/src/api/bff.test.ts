import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { z } from 'zod'
import { bffJson } from '@/api/bff'
import { ApiError } from '@/lib/problem-details'
import { messageFor } from '@/lib/problem-messages'
import { server } from '@/test/server'

describe('bffJson', () => {
  const schema = z.object({ email: z.string() })

  it('returns the parsed body', async () => {
    server.use(http.get('*/bff/user', () => HttpResponse.json({ email: 'a@example.test' })))
    await expect(bffJson('/bff/user', schema)).resolves.toEqual({ email: 'a@example.test' })
  })

  it('turns a body it does not recognise into an ApiError with the invalid-response slug', async () => {
    server.use(http.get('*/bff/user', () => HttpResponse.json({ nope: 1 })))
    const error = await bffJson('/bff/user', schema).catch((cause: unknown) => cause)
    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).slug).toBe('invalid-response')
    expect(messageFor(error as ApiError)).toBe('Something went wrong. Try again.')
  })

  it('does the same for a body that is not JSON', async () => {
    server.use(http.get('*/bff/user', () => new HttpResponse('<html>', { status: 200 })))
    const error = await bffJson('/bff/user', schema).catch((cause: unknown) => cause)
    expect((error as ApiError).slug).toBe('invalid-response')
  })
})
