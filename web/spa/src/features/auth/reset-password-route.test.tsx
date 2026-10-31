import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { problem } from '@/test/problem'
import { renderApp } from '@/test/render-app'
import { server } from '@/test/server'

// Generated sample data only.
const email = 'sample.person@example.test'
const token = 'sample-reset-token-xyz789'
const link = `/reset-password?email=${encodeURIComponent(email)}&token=${token}`
const newPassword = 'a brand new passphrase'

function openLink(path: string) {
  window.history.replaceState(null, '', path)
  return renderApp(path)
}

afterEach(() => {
  window.history.replaceState(null, '', '/')
})

async function submitNewPassword(value = newPassword) {
  const user = userEvent.setup()
  await user.type(await screen.findByLabelText('New password'), value)
  await user.click(screen.getByRole('button', { name: 'Set new password' }))
  return user
}

describe('reset password page', () => {
  it('sets the password and sends the user to sign-in with "Password changed" [US-F025 AC1]', async () => {
    let body: unknown
    let csrf: string | null = null
    server.use(
      http.post('*/api/v1/auth/reset-password', async ({ request }) => {
        csrf = request.headers.get('X-CSRF')
        body = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    openLink(link)

    await submitNewPassword()

    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeVisible()
    expect(screen.getByText('Password changed. Sign in with your new password.')).toBeVisible()
    expect(body).toEqual({ email, token, newPassword })
    expect(csrf).toBe('1')
  })

  it('shows the email read-only for password managers and allows paste [AN-8, NFR-F02]', async () => {
    openLink(link)
    const user = userEvent.setup()

    const emailField = await screen.findByLabelText('Email')
    expect(emailField).toHaveValue(email)
    expect(emailField).toHaveAttribute('readonly')
    expect(emailField).toHaveAttribute('autocomplete', 'username')

    const password = screen.getByLabelText('New password')
    expect(password).toHaveAttribute('autocomplete', 'new-password')
    await user.click(password)
    await user.paste('pasted passphrase value')
    expect(password).toHaveValue('pasted passphrase value')
  })

  it('removes email and token from the address bar after reading them [US-F025 AC3]', async () => {
    const original = window.history.replaceState.bind(window.history)
    const spy = vi.spyOn(window.history, 'replaceState').mockImplementation((...args) => {
      original(...args)
    })
    window.history.replaceState(null, '', link)
    spy.mockClear()
    renderApp(link)

    await screen.findByRole('heading', { level: 1, name: 'Set a new password' })

    expect(spy).toHaveBeenCalledTimes(1)
    expect(window.location.search).toBe('')
    expect(window.location.href).not.toContain(token)
    expect(window.location.href).not.toContain('example.test')
    // The form still has what it needs, from memory.
    expect(screen.getByLabelText('Email')).toHaveValue(email)
  })

  it('shows "This link is invalid or expired" and a link to request a new one [US-F025 AC2]', async () => {
    server.use(http.post('*/api/v1/auth/reset-password', () => problem(400, 'invalid-token')))
    openLink(link)

    await submitNewPassword()

    const message = await screen.findByText('This link is invalid or expired.')
    expect(message.closest('[role="alert"]')).not.toBeNull()
    expect(screen.getByRole('link', { name: 'Request new link' })).toHaveAttribute(
      'href',
      '/forgot-password',
    )
    expect(screen.queryByLabelText('New password')).not.toBeInTheDocument()
  })

  it('asks to open the email link again when the token is missing (page refreshed) [AN-8]', async () => {
    openLink('/reset-password')

    expect(await screen.findByText(/Open the link from your email again/)).toBeVisible()
    expect(screen.getByRole('link', { name: 'Request new link' })).toHaveAttribute(
      'href',
      '/forgot-password',
    )
    expect(screen.queryByLabelText('New password')).not.toBeInTheDocument()
  })

  it('validates the 15 character rule before sending', async () => {
    // No handler for EP-11: an unexpected call would fail the test.
    openLink(link)

    await submitNewPassword('too short')

    const password = screen.getByLabelText('New password')
    await waitFor(() => {
      expect(password).toHaveAccessibleDescription(/Use at least 15 characters/)
    })
    expect(password).toHaveFocus()
  })

  it('maps a server password error to the field', async () => {
    server.use(
      http.post('*/api/v1/auth/reset-password', () =>
        problem(400, 'validation-failed', {
          errors: { newPassword: ['This password is too common. Choose another.'] },
        }),
      ),
    )
    openLink(link)

    await submitNewPassword()

    const password = await screen.findByLabelText('New password')
    await waitFor(() => {
      expect(password).toHaveAccessibleDescription(/This password is too common/)
    })
    expect(password).toHaveFocus()
  })

  it('disables the button while the request runs and during a 429 countdown [NFR-F23, F24]', async () => {
    let release: () => void = () => undefined
    const gate = new Promise<void>((resolve) => {
      release = resolve
    })
    server.use(
      http.post('*/api/v1/auth/reset-password', async () => {
        await gate
        return problem(429, 'rate-limited', {}, { 'Retry-After': '30' })
      }),
    )
    openLink(link)

    await submitNewPassword()

    expect(await screen.findByRole('button', { name: 'Saving…' })).toBeDisabled()
    release()
    expect(await screen.findByText('Too many attempts. Try again in 30 seconds.')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Set new password' })).toBeDisabled()
  })
})
