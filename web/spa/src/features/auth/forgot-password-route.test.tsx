import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http } from 'msw'
import { describe, expect, it } from 'vitest'
import { FORGOT_RESULT } from '@/features/auth/forgot-password-route'
import { accepted, problem } from '@/test/problem'
import { renderApp } from '@/test/render-app'
import { server } from '@/test/server'

describe('forgot password page', () => {
  it('shows the same result for any email [US-F024 AC1]', async () => {
    const emails: string[] = []
    server.use(
      http.post('*/api/v1/auth/forgot-password', async ({ request }) => {
        emails.push(((await request.json()) as { email: string }).email)
        return accepted()
      }),
    )
    renderApp('/forgot-password')
    const user = userEvent.setup()
    const email = await screen.findByLabelText('Email')

    await user.type(email, 'known.person@example.test')
    await user.click(screen.getByRole('button', { name: 'Send reset link' }))
    const result = await screen.findByText(FORGOT_RESULT)
    expect(result).toHaveAttribute('role', 'status')

    await user.clear(email)
    await user.type(email, 'nobody.at.all@example.test')
    await user.click(screen.getByRole('button', { name: 'Send reset link' }))
    expect(await screen.findByText(FORGOT_RESULT)).toBeVisible()
    expect(emails).toEqual(['known.person@example.test', 'nobody.at.all@example.test'])
  })

  it('shows a field error and makes no call for an invalid email format [US-F024 AC2]', async () => {
    // No handler for EP-10: an unexpected call would fail the test.
    renderApp('/forgot-password')
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText('Email'), 'not-an-email')
    await user.click(screen.getByRole('button', { name: 'Send reset link' }))

    const email = screen.getByLabelText('Email')
    expect(email).toHaveAccessibleDescription('Enter a valid email address.')
    expect(email).toHaveAttribute('aria-invalid', 'true')
    expect(email).toHaveFocus()
    expect(screen.queryByText(FORGOT_RESULT)).not.toBeInTheDocument()
  })

  it('shows the 429 countdown and disables the button [NFR-F23]', async () => {
    server.use(
      http.post('*/api/v1/auth/forgot-password', () =>
        problem(429, 'rate-limited', {}, { 'Retry-After': '60' }),
      ),
    )
    renderApp('/forgot-password')
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText('Email'), 'sample.person@example.test')
    await user.click(screen.getByRole('button', { name: 'Send reset link' }))

    expect(await screen.findByText('Too many attempts. Try again in 60 seconds.')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Send reset link' })).toBeDisabled()
  })

  it('shows a server field error under the email field', async () => {
    server.use(
      http.post('*/api/v1/auth/forgot-password', () =>
        problem(400, 'validation-failed', { errors: { email: ['Email is not valid.'] } }),
      ),
    )
    renderApp('/forgot-password')
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText('Email'), 'sample.person@example.test')
    await user.click(screen.getByRole('button', { name: 'Send reset link' }))

    const email = screen.getByLabelText('Email')
    await screen.findByText('Email is not valid.')
    expect(email).toHaveAccessibleDescription('Email is not valid.')
    expect(email).toHaveFocus()
  })

  it('is reachable from the sign-in page and links back', async () => {
    renderApp('/sign-in')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('link', { name: 'Forgot password?' }))
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Reset your password' }),
    ).toBeVisible()

    await user.click(screen.getByRole('link', { name: 'Back to sign in' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeVisible()
  })
})
