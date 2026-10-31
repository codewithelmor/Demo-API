import { describe, expect, it } from 'vitest'
import { ApiError } from '@/lib/problem-details'
import { messageFor, referenceFor } from '@/lib/problem-messages'

function error(status: number, slug: string, retryAfter?: number) {
  return new ApiError(
    { type: `/problems/${slug}`, title: `Title ${slug}`, status, traceId: 'trace-1' },
    retryAfter,
  )
}

describe('problem messages for the access endpoints (EP-01, 02, 03, 10, 11)', () => {
  it('maps every slug these endpoints can return', () => {
    expect(messageFor(error(400, 'validation-failed'))).toBe(
      'Check the highlighted fields and try again.',
    )
    expect(messageFor(error(400, 'invalid-token'))).toBe('This link is invalid or expired.')
    expect(messageFor(error(429, 'rate-limited', 30))).toBe(
      'Too many attempts. Try again in 30 seconds.',
    )
    expect(messageFor(error(500, 'internal-error'))).toBe('Something went wrong. Try again.')
    expect(messageFor(error(503, 'service-unavailable'))).toBe(
      'The service is busy. Try again in a moment.',
    )
    for (const [status, slug] of [
      [413, 'payload-too-large'],
      [415, 'unsupported-media-type'],
      [405, 'method-not-allowed'],
    ] as const) {
      expect(messageFor(error(status, slug))).toBe('Something went wrong on our side.')
    }
  })

  it('falls back to the title for an unknown slug and shows the reference', () => {
    const unknown = error(418, 'teapot')
    expect(messageFor(unknown)).toBe('Title teapot')
    expect(referenceFor(unknown)).toBe('trace-1')
  })

  it('shows the reference for server errors and client bugs, not for expected problems', () => {
    expect(referenceFor(error(500, 'internal-error'))).toBe('trace-1')
    expect(referenceFor(error(415, 'unsupported-media-type'))).toBe('trace-1')
    expect(referenceFor(error(400, 'invalid-token'))).toBeUndefined()
    expect(referenceFor(error(400, 'validation-failed'))).toBeUndefined()
    expect(referenceFor(error(429, 'rate-limited', 5))).toBeUndefined()
  })
})

describe('problem messages for the account endpoints (EP-35, 36, 37)', () => {
  it('maps the conflict and open-orders slugs', () => {
    expect(messageFor(error(412, 'precondition-failed'))).toBe(
      'This record changed since you opened it.',
    )
    expect(messageFor(error(409, 'customer-has-open-orders'))).toBe(
      'Cancel or finish your open orders first.',
    )
  })

  it('maps the write race, the dead refresh token and the unknown response', () => {
    expect(messageFor(error(409, 'concurrency-conflict'))).toBe(
      'Someone changed this at the same time. We reloaded it. Try again.',
    )
    expect(messageFor(error(401, 'invalid-refresh-token'))).toBe(
      'Your session ended. Sign in again to continue.',
    )
    expect(messageFor(error(0, 'invalid-response'))).toBe('Something went wrong. Try again.')
    expect(referenceFor(error(409, 'email-change-not-allowed'))).toBe('trace-1')
  })

  it('treats 428 as a client bug: generic text and the reference', () => {
    const missing = error(428, 'precondition-required')
    expect(messageFor(missing)).toBe('Something went wrong on our side.')
    expect(referenceFor(missing)).toBe('trace-1')
  })
})
