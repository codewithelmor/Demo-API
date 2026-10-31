import { describe, expect, it } from 'vitest'
import { apiErrorFrom, fieldErrors, slugOf } from '@/lib/problem-details'

describe('problem details', () => {
  it('reads the slug from the type', () => {
    expect(slugOf({ type: '/problems/invalid-credentials' })).toBe('invalid-credentials')
    expect(slugOf({ type: 'https://example.test/problems/not-found' })).toBe('not-found')
    expect(slugOf({ type: 'about:blank' })).toBeUndefined()
    expect(slugOf({})).toBeUndefined()
  })

  it('maps error keys to camelCase field names and keeps the first message', () => {
    expect(
      fieldErrors({
        errors: { Email: ['Bad email', 'Other'], password: ['Too short'], '': ['x'] },
      }),
    ).toEqual({ email: 'Bad email', password: 'Too short' })
  })

  it('builds an ApiError from a ProblemDetails body and Retry-After', () => {
    const response = new Response(null, { status: 429, headers: { 'Retry-After': '30' } })
    const error = apiErrorFrom(
      { type: '/problems/rate-limited', title: 'Slow down', traceId: 't1' },
      response,
    )
    expect(error.status).toBe(429)
    expect(error.slug).toBe('rate-limited')
    expect(error.retryAfterSeconds).toBe(30)
    expect(error.problem.traceId).toBe('t1')
  })

  it('falls back to the HTTP status when the body is not a problem', () => {
    const error = apiErrorFrom(
      '<html>oops</html>',
      new Response(null, { status: 502, statusText: 'Bad Gateway' }),
    )
    expect(error.status).toBe(502)
    expect(error.problem.title).toBe('Bad Gateway')
    expect(error.slug).toBeUndefined()
  })
})
