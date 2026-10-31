import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { RESEND_RESULT } from '@/features/auth/resend-confirmation-form'
import { accepted, problem } from '@/test/problem'
import { renderApp } from '@/test/render-app'
import { server } from '@/test/server'

// Generated sample data only.
const userId = '5b1b7c9e-0000-4000-8000-0000000000aa'
const token = 'sample-confirm-token-abc123'
const link = `/confirm-email?userId=${userId}&token=${token}`

// Opens the link like a browser does: the address bar has the query string before the app starts.
function openLink(path: string) {
  window.history.replaceState(null, '', path)
  return renderApp(path)
}

afterEach(() => {
  window.history.replaceState(null, '', '/')
})

describe('confirm email page', () => {
  it('shows "Email confirmed" and a sign-in button for a valid link [US-F021 AC1]', async () => {
    let body: unknown
    server.use(
      http.post('*/api/v1/auth/confirm-email', async ({ request }) => {
        body = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    openLink(link)

    expect(await screen.findByRole('heading', { level: 1, name: 'Email confirmed' })).toBeVisible()
    expect(screen.getByText('Email confirmed. You can now sign in.')).toBeVisible()
    expect(screen.getByRole('link', { name: 'Sign in' })).toHaveAttribute('href', '/sign-in')
    expect(body).toEqual({ userId, token })
  })

  it('removes userId and token from the address bar before it posts [US-F021 NFR-F43]', async () => {
    const events: string[] = []
    let addressAtPost: string | undefined
    const original = window.history.replaceState.bind(window.history)
    vi.spyOn(window.history, 'replaceState').mockImplementation((...args) => {
      events.push('replaceState')
      original(...args)
    })
    server.use(
      http.post('*/api/v1/auth/confirm-email', () => {
        events.push('post')
        addressAtPost = window.location.search
        return new HttpResponse(null, { status: 204 })
      }),
    )
    // openLink itself calls replaceState once to set up the address bar; start counting after it.
    window.history.replaceState(null, '', link)
    events.length = 0
    renderApp(link)

    await screen.findByRole('heading', { level: 1, name: 'Email confirmed' })

    expect(events).toEqual(['replaceState', 'post'])
    expect(addressAtPost).toBe('')
    expect(window.location.search).toBe('')
    expect(window.location.href).not.toContain(token)
  })

  it('posts only once per visit [US-F021]', async () => {
    let calls = 0
    server.use(
      http.post('*/api/v1/auth/confirm-email', () => {
        calls++
        return new HttpResponse(null, { status: 204 })
      }),
    )
    openLink(link)

    await screen.findByRole('heading', { level: 1, name: 'Email confirmed' })

    expect(calls).toBe(1)
  })

  it('still shows "Email confirmed" when the account was already confirmed (204 again) [US-F021 AC3]', async () => {
    server.use(
      http.post('*/api/v1/auth/confirm-email', () => new HttpResponse(null, { status: 204 })),
    )
    openLink(link)

    expect(await screen.findByRole('heading', { level: 1, name: 'Email confirmed' })).toBeVisible()
  })

  it('shows "This link is invalid or expired" with a resend form for invalid-token [US-F021 AC2]', async () => {
    server.use(http.post('*/api/v1/auth/confirm-email', () => problem(400, 'invalid-token')))
    openLink(link)

    expect(
      await screen.findByRole('heading', { level: 1, name: 'This link is invalid or expired' }),
    ).toBeVisible()
    expect(screen.getByLabelText('Email')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Send new link' })).toBeVisible()
  })

  it('resends from the invalid-link page with the same neutral result [US-F021 AC2, US-F022]', async () => {
    let body: unknown
    server.use(
      http.post('*/api/v1/auth/confirm-email', () => problem(400, 'invalid-token')),
      http.post('*/api/v1/auth/resend-confirmation', async ({ request }) => {
        body = await request.json()
        return accepted()
      }),
    )
    openLink(link)
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText('Email'), 'sample.person@example.test')
    await user.click(screen.getByRole('button', { name: 'Send new link' }))

    expect(await screen.findByText(RESEND_RESULT)).toBeVisible()
    expect(body).toEqual({ email: 'sample.person@example.test' })
  })

  it('treats missing parameters as an invalid link and posts nothing [US-F021 AC2]', async () => {
    // No handler for EP-02: an unexpected call would fail the test.
    openLink('/confirm-email?userId=not-a-guid')

    expect(
      await screen.findByRole('heading', { level: 1, name: 'This link is invalid or expired' }),
    ).toBeVisible()
    expect(window.location.search).toBe('')
  })

  it('shows a 429 countdown and keeps Try again disabled [AN-5]', async () => {
    server.use(
      http.post('*/api/v1/auth/confirm-email', () =>
        problem(429, 'rate-limited', {}, { 'Retry-After': '30' }),
      ),
    )
    openLink(link)

    const message = await screen.findByText('Too many attempts. Try again in 30 seconds.')
    expect(message.closest('[role="alert"]')).not.toBeNull()
    expect(screen.getByRole('button', { name: 'Try again' })).toBeDisabled()
  })

  it('tries again after a server error with the same link values [AN-5]', async () => {
    const bodies: unknown[] = []
    server.use(
      http.post('*/api/v1/auth/confirm-email', async ({ request }) => {
        bodies.push(await request.json())
        return bodies.length === 1
          ? problem(503, 'service-unavailable')
          : new HttpResponse(null, { status: 204 })
      }),
    )
    openLink(link)
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Email confirmed' })).toBeVisible()
    expect(bodies).toEqual([
      { userId, token },
      { userId, token },
    ])
  })
})
