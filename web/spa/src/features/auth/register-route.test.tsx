import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http } from 'msw'
import { describe, expect, it } from 'vitest'
import { accepted, problem } from '@/test/problem'
import { renderApp } from '@/test/render-app'
import { server } from '@/test/server'

// Generated sample data only.
const person = {
  firstName: 'Sample',
  lastName: 'Person',
  email: 'sample.person@example.test',
  password: 'correct horse battery staple',
  phone: '+14155550123',
}

async function fill(values: Partial<typeof person> = {}) {
  const data = { ...person, ...values }
  const user = userEvent.setup()
  await user.type(await screen.findByLabelText('First name'), data.firstName)
  await user.type(screen.getByLabelText('Last name'), data.lastName)
  await user.type(screen.getByLabelText('Email'), data.email)
  await user.type(screen.getByLabelText('Password'), data.password)
  if (data.phone) await user.type(screen.getByLabelText('Phone (optional)'), data.phone)
  return user
}

const createButton = () => screen.getByRole('button', { name: /^Create account|Creating account/ })

describe('register page', () => {
  it('shows "Check your email" after a 202 and sends the body with the CSRF header [US-F020 AC1]', async () => {
    let csrf: string | null = null
    let body: unknown
    server.use(
      http.post('*/api/v1/auth/register', async ({ request }) => {
        csrf = request.headers.get('X-CSRF')
        body = await request.json()
        return accepted()
      }),
    )
    renderApp('/register')
    const user = await fill()

    await user.click(createButton())

    expect(await screen.findByRole('heading', { level: 1, name: 'Check your email' })).toBeVisible()
    expect(
      screen.getByText(/If this email can be used, we sent a link to confirm your account/),
    ).toBeVisible()
    expect(csrf).toBe('1')
    expect(body).toEqual(person)
  })

  it('does not send an empty optional phone [US-F020 AC1]', async () => {
    let body: Record<string, unknown> = {}
    server.use(
      http.post('*/api/v1/auth/register', async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>
        return accepted()
      }),
    )
    renderApp('/register')
    const user = await fill({ phone: '' })

    await user.click(createButton())

    await screen.findByRole('heading', { level: 1, name: 'Check your email' })
    expect(body).not.toHaveProperty('phone')
  })

  it('shows the 15 character rule when leaving a short password, and sends nothing [US-F020 AC2]', async () => {
    let calls = 0
    server.use(
      http.post('*/api/v1/auth/register', () => {
        calls++
        return accepted()
      }),
    )
    renderApp('/register')
    const user = userEvent.setup()
    const password = await screen.findByLabelText('Password')
    await user.type(password, 'too short')
    await user.tab()

    await waitFor(() => {
      expect(password).toHaveAccessibleDescription(/Use at least 15 characters/)
    })
    expect(password).toHaveAttribute('aria-invalid', 'true')
    // A live character counter appears after the first time the user leaves the field.
    expect(password).toHaveAccessibleDescription(/9 of 15-128 characters/)

    await user.click(createButton())
    expect(calls).toBe(0)
  })

  it('maps a blocklist 400 to the password field and focuses it [US-F020 AC3]', async () => {
    server.use(
      http.post('*/api/v1/auth/register', () =>
        problem(400, 'validation-failed', {
          errors: { password: ['This password is too common. Choose another.'] },
        }),
      ),
    )
    renderApp('/register')
    const user = await fill()

    await user.click(createButton())

    const password = await screen.findByLabelText('Password')
    await waitFor(() => {
      expect(password).toHaveAccessibleDescription(/This password is too common/)
    })
    expect(password).toHaveAttribute('aria-invalid', 'true')
    expect(password).toHaveFocus()
    expect(screen.getByRole('button', { name: 'Create account' })).toBeEnabled()
  })

  it('shows the E.164 hint for a phone without the country code [US-F020 AC4]', async () => {
    let calls = 0
    server.use(
      http.post('*/api/v1/auth/register', () => {
        calls++
        return accepted()
      }),
    )
    renderApp('/register')
    const user = await fill({ phone: '4155550123' })

    await user.click(createButton())

    const phone = screen.getByLabelText('Phone (optional)')
    await waitFor(() => {
      expect(phone).toHaveAccessibleDescription(/with \+ and the country code/)
    })
    expect(phone).toHaveFocus()
    expect(calls).toBe(0)
  })

  it('announces an error summary and focuses the first invalid field [NFR-F03]', async () => {
    renderApp('/register')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Create account' }))

    const summary = await screen.findByText('Fix 4 problems to continue')
    expect(summary.closest('[role="alert"]')).not.toBeNull()
    expect(screen.getByLabelText('First name')).toHaveFocus()
    // A summary entry moves focus to its field.
    await user.click(screen.getByRole('button', { name: /^Email: / }))
    expect(screen.getByLabelText('Email')).toHaveFocus()
  })

  it('shows unknown server error keys in the form alert', async () => {
    server.use(
      http.post('*/api/v1/auth/register', () =>
        problem(400, 'validation-failed', { errors: { body: ['The request body is not valid.'] } }),
      ),
    )
    renderApp('/register')
    const user = await fill()

    await user.click(createButton())

    const alert = await screen.findByText('The request body is not valid.')
    expect(alert.closest('[role="alert"]')).not.toBeNull()
  })

  it('disables the button while the request runs [NFR-F24]', async () => {
    let release: () => void = () => undefined
    const gate = new Promise<void>((resolve) => {
      release = resolve
    })
    let calls = 0
    server.use(
      http.post('*/api/v1/auth/register', async () => {
        calls++
        await gate
        return accepted()
      }),
    )
    renderApp('/register')
    const user = await fill()

    await user.click(createButton())

    const busy = await screen.findByRole('button', { name: 'Creating account…' })
    expect(busy).toBeDisabled()
    await user.click(busy)
    release()
    await screen.findByRole('heading', { level: 1, name: 'Check your email' })
    expect(calls).toBe(1)
  })

  it('disables the button during a 429 countdown [NFR-F23]', async () => {
    server.use(
      http.post('*/api/v1/auth/register', () =>
        problem(429, 'rate-limited', {}, { 'Retry-After': '30' }),
      ),
    )
    renderApp('/register')
    const user = await fill()

    await user.click(createButton())

    const alert = await screen.findByText('Too many attempts. Try again in 30 seconds.')
    expect(alert.closest('[role="alert"]')).not.toBeNull()
    expect(screen.getByRole('button', { name: 'Create account' })).toBeDisabled()
  })

  it('shows a generic message and the reference for a 500', async () => {
    server.use(http.post('*/api/v1/auth/register', () => problem(500, 'internal-error')))
    renderApp('/register')
    const user = await fill()

    await user.click(createButton())

    const message = await screen.findByText('Something went wrong. Try again.')
    expect(message.closest('[role="alert"]')).not.toBeNull()
    expect(screen.getByText('Reference: trace-test-123')).toBeVisible()
  })

  it('allows paste and a show/hide toggle in the password field [NFR-F02]', async () => {
    renderApp('/register')
    const user = userEvent.setup()
    const password = await screen.findByLabelText('Password')

    await user.click(password)
    await user.paste('pasted passphrase value')
    expect(password).toHaveValue('pasted passphrase value')
    expect(password).toHaveAttribute('type', 'password')
    expect(password).toHaveAttribute('autocomplete', 'new-password')

    await user.click(screen.getByRole('button', { name: 'Show password' }))
    expect(password).toHaveAttribute('type', 'text')
    expect(screen.getByRole('button', { name: 'Hide password' })).toBeVisible()
  })

  it('links to sign-in and back', async () => {
    renderApp('/register')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('link', { name: 'Sign in' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeVisible()
    expect(screen.getByRole('link', { name: 'Create account' })).toHaveAttribute(
      'href',
      '/register',
    )
    expect(screen.getByRole('link', { name: 'Forgot password?' })).toHaveAttribute(
      'href',
      '/forgot-password',
    )
  })
})
