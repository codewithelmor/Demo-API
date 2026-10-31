import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { RESEND_RESULT } from '@/features/auth/resend-confirmation-form'
import { RESEND_SENT } from '@/features/auth/unconfirmed-alert'
import { accepted, problem } from '@/test/problem'
import { renderApp } from '@/test/render-app'
import { server } from '@/test/server'

async function signInUnconfirmed() {
  const user = userEvent.setup()
  await user.type(await screen.findByLabelText('Email'), 'sample.person@example.test')
  await user.type(screen.getByLabelText('Password'), 'correct horse battery staple')
  await user.click(screen.getByRole('button', { name: 'Sign in' }))
  return user
}

describe('resend confirmation', () => {
  it('shows the same neutral result for any email on the page [US-F022 AC1]', async () => {
    const emails: string[] = []
    server.use(
      http.post('*/api/v1/auth/resend-confirmation', async ({ request }) => {
        emails.push(((await request.json()) as { email: string }).email)
        return accepted()
      }),
    )
    renderApp('/resend-confirmation')
    const user = userEvent.setup()
    const email = await screen.findByLabelText('Email')

    await user.type(email, 'known.person@example.test')
    await user.click(screen.getByRole('button', { name: 'Send confirmation email' }))
    expect(await screen.findByText(RESEND_RESULT)).toBeVisible()

    await user.clear(email)
    await user.type(email, 'nobody.at.all@example.test')
    await user.click(screen.getByRole('button', { name: 'Send confirmation email' }))
    expect(await screen.findByText(RESEND_RESULT)).toBeVisible()
    expect(emails).toEqual(['known.person@example.test', 'nobody.at.all@example.test'])
  })

  it('shows a field error and sends nothing for a bad email', async () => {
    renderApp('/resend-confirmation')
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText('Email'), 'not-an-email')
    await user.click(screen.getByRole('button', { name: 'Send confirmation email' }))

    const email = screen.getByLabelText('Email')
    expect(email).toHaveAccessibleDescription('Enter a valid email address.')
    expect(email).toHaveFocus()
  })

  it('offers "Resend email" after 403 email-not-confirmed and reuses the typed email [US-F022 AC1]', async () => {
    let body: unknown
    server.use(
      http.post('*/bff/login', () => problem(403, 'email-not-confirmed')),
      http.post('*/api/v1/auth/resend-confirmation', async ({ request }) => {
        body = await request.json()
        return accepted()
      }),
    )
    renderApp('/sign-in')
    const user = await signInUnconfirmed()

    expect(await screen.findByText(/Confirm your email first/)).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Resend email' }))

    const alert = await screen.findByText(RESEND_SENT)
    expect(alert.closest('[role="alert"]')).not.toBeNull()
    // The button that had focus is gone: focus moves to the result.
    expect(alert).toHaveFocus()
    expect(screen.queryByRole('button', { name: 'Resend email' })).not.toBeInTheDocument()
    expect(body).toEqual({ email: 'sample.person@example.test' })
  })

  it('shows the 429 countdown on the page [US-F022 AC2]', async () => {
    server.use(
      http.post('*/api/v1/auth/resend-confirmation', () =>
        problem(429, 'rate-limited', {}, { 'Retry-After': '45' }),
      ),
    )
    renderApp('/resend-confirmation')
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText('Email'), 'sample.person@example.test')
    await user.click(screen.getByRole('button', { name: 'Send confirmation email' }))

    const message = await screen.findByText('Too many attempts. Try again in 45 seconds.')
    expect(message.closest('[role="alert"]')).not.toBeNull()
    expect(screen.getByRole('button', { name: 'Send confirmation email' })).toBeDisabled()
  })

  it('shows the 429 countdown next to "Resend email" on sign-in [US-F022 AC2]', async () => {
    server.use(
      http.post('*/bff/login', () => problem(403, 'email-not-confirmed')),
      http.post('*/api/v1/auth/resend-confirmation', () =>
        problem(429, 'rate-limited', {}, { 'Retry-After': '20' }),
      ),
    )
    renderApp('/sign-in')
    const user = await signInUnconfirmed()

    await user.click(await screen.findByRole('button', { name: 'Resend email' }))

    expect(await screen.findByText('Too many attempts. Try again in 20 seconds.')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Resend email' })).toBeDisabled()
  })

  it('tells the user when the resend request cannot reach the server', async () => {
    server.use(
      http.post('*/bff/login', () => problem(403, 'email-not-confirmed')),
      http.post('*/api/v1/auth/resend-confirmation', () => HttpResponse.error()),
    )
    renderApp('/sign-in')
    const user = await signInUnconfirmed()

    await user.click(await screen.findByRole('button', { name: 'Resend email' }))

    expect(
      await screen.findByText('Could not reach the server. Check your connection and try again.'),
    ).toBeVisible()
    expect(screen.getByRole('button', { name: 'Resend email' })).toBeEnabled()
  })

  it('is reachable from the sign-in footer', async () => {
    renderApp('/sign-in')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('link', { name: 'Resend it' }))

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Resend confirmation email' }),
    ).toBeVisible()
  })
})
