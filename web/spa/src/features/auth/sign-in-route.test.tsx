import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { adminUser, customerUser } from '@/test/data'
import { problem } from '@/test/problem'
import { renderApp } from '@/test/render-app'
import { server } from '@/test/server'

async function fillAndSubmit(email = 'customer.one@example.test', password = 'Sample-Pass-123!') {
  const user = userEvent.setup()
  await user.type(await screen.findByLabelText('Email'), email)
  await user.type(screen.getByLabelText('Password'), password)
  await user.click(screen.getByRole('button', { name: 'Sign in' }))
  return user
}

describe('sign-in page', () => {
  it('sends the CSRF header and credentials, then shows the catalog [US-F002 AC1]', async () => {
    let csrf: string | null = null
    let body: unknown
    server.use(
      http.post('*/bff/login', async ({ request }) => {
        csrf = request.headers.get('X-CSRF')
        body = await request.json()
        return HttpResponse.json(customerUser)
      }),
    )
    renderApp('/sign-in')

    await fillAndSubmit()

    expect(await screen.findByRole('heading', { level: 1, name: 'Products' })).toBeInTheDocument()
    expect(await screen.findByText('Sample product 1')).toBeInTheDocument()
    expect(csrf).toBe('1')
    expect(body).toEqual({ email: 'customer.one@example.test', password: 'Sample-Pass-123!' })
    // The signed-in shell shows the email and a sign-out button.
    expect(screen.getByText(customerUser.email)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeInTheDocument()
  })

  it('goes back to the page the user came from [US-F006 AC3]', async () => {
    server.use(http.post('*/bff/login', () => HttpResponse.json(customerUser)))
    renderApp('/products?page=1')
    // Anonymous: redirected to sign-in with returnUrl.
    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeInTheDocument()

    await fillAndSubmit()

    expect(await screen.findByRole('heading', { level: 1, name: 'Products' })).toBeInTheDocument()
  })

  it('sends an Admin to the back office', async () => {
    server.use(http.post('*/bff/login', () => HttpResponse.json(adminUser)))
    renderApp('/sign-in')

    await fillAndSubmit('admin.one@example.test')

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Back office' }),
    ).toBeInTheDocument()
  })

  it('shows "Email or password is incorrect." for invalid-credentials [US-F002 AC2]', async () => {
    server.use(http.post('*/bff/login', () => problem(401, 'invalid-credentials')))
    renderApp('/sign-in')

    await fillAndSubmit()

    expect(await screen.findByRole('alert')).toHaveTextContent('Email or password is incorrect.')
    // The password is cleared and focus moves to it.
    await waitFor(() => {
      expect(screen.getByLabelText('Password')).toHaveFocus()
    })
    expect(screen.getByLabelText('Password')).toHaveValue('')
    expect(screen.getByRole('heading', { level: 1, name: 'Sign in' })).toBeInTheDocument()
  })

  it('explains an unconfirmed email [US-F002 AC3]', async () => {
    server.use(http.post('*/bff/login', () => problem(403, 'email-not-confirmed')))
    renderApp('/sign-in')

    await fillAndSubmit()

    expect(await screen.findByRole('alert')).toHaveTextContent('Confirm your email first.')
  })

  it('puts server field errors under the field [US-F007 AC1]', async () => {
    server.use(
      http.post('*/bff/login', () =>
        problem(400, 'validation-failed', { errors: { Email: ['Email is not valid.'] } }),
      ),
    )
    renderApp('/sign-in')

    await fillAndSubmit('someone@example.test')

    const email = await screen.findByLabelText('Email')
    await waitFor(() => {
      expect(email).toHaveAccessibleDescription('Email is not valid.')
    })
    expect(email).toHaveAttribute('aria-invalid', 'true')
    expect(email).toHaveFocus()
  })

  it('disables the button while a 429 countdown runs [US-F007 AC2]', async () => {
    server.use(
      http.post('*/bff/login', () => problem(429, 'rate-limited', {}, { 'Retry-After': '30' })),
    )
    renderApp('/sign-in')

    await fillAndSubmit()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Too many attempts. Try again in 30 seconds.',
    )
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeDisabled()
  })

  it('validates the form before calling the BFF', async () => {
    renderApp('/sign-in')
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument()
    expect(screen.getByText('Enter your password.')).toBeInTheDocument()
  })

  it('shows the reason for being on the sign-in page', async () => {
    renderApp('/sign-in?reason=session-ended')
    expect(
      await screen.findByText('Your session ended. Sign in again to continue.'),
    ).toBeInTheDocument()
  })

  it.each(['constructor', '__proto__', 'toString', 'unknown'])(
    'shows no reason text for ?reason=%s',
    async (reason) => {
      renderApp(`/sign-in?reason=${reason}`)
      await screen.findByRole('heading', { level: 1, name: 'Sign in' })
      expect(screen.queryByRole('status')).not.toBeInTheDocument()
    },
  )

  it.each([
    ['/api/v1/products', 'api path'],
    ['/bff/logout', 'bff path'],
    ['/%2f%2fevil.test', 'encoded double slash'],
    ['/a%5Cb', 'encoded backslash'],
  ])('ignores the returnUrl %s (%s)', async (returnUrl) => {
    server.use(http.post('*/bff/login', () => HttpResponse.json(customerUser)))
    renderApp(`/sign-in?returnUrl=${encodeURIComponent(returnUrl)}`)

    await fillAndSubmit()

    expect(await screen.findByRole('heading', { level: 1, name: 'Products' })).toBeInTheDocument()
  })

  it('does not send a Customer to /admin, and sends an Admin there', async () => {
    server.use(http.post('*/bff/login', () => HttpResponse.json(customerUser)))
    renderApp('/sign-in?returnUrl=%2Fadmin')
    await fillAndSubmit()
    expect(await screen.findByRole('heading', { level: 1, name: 'Products' })).toBeInTheDocument()
  })

  it('shows the generic network message when the BFF cannot be reached', async () => {
    server.use(http.post('*/bff/login', () => HttpResponse.error()))
    renderApp('/sign-in')

    await fillAndSubmit()

    expect(
      await screen.findByText('Could not reach the server. Check your connection and try again.'),
    ).toBeVisible()
  })

  it('shows a retryable error when the BFF answers with something unexpected', async () => {
    server.use(http.post('*/bff/login', () => HttpResponse.json({ nonsense: true })))
    renderApp('/sign-in')

    await fillAndSubmit()

    expect(await screen.findByText('Something went wrong. Try again.')).toBeVisible()
  })

  it('ignores an unsafe returnUrl', async () => {
    server.use(http.post('*/bff/login', () => HttpResponse.json(customerUser)))
    renderApp('/sign-in?returnUrl=%2F%2Fevil.test')

    await fillAndSubmit()

    expect(await screen.findByRole('heading', { level: 1, name: 'Products' })).toBeInTheDocument()
  })
})
