import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { Profile } from '@/features/profile/profile-api'
import { formatDateTime } from '@/lib/dates'
import { cartKeyFor, readCart, writeCart } from '@/lib/cart-storage'
import { adminUser, customerUser, productId } from '@/test/data'
import { problem } from '@/test/problem'
import { renderApp } from '@/test/render-app'
import { server } from '@/test/server'
import { signedInAs } from '@/test/session'

if (!customerUser.customerId) throw new Error('Test customer needs a customer ID.')
const cartKey = cartKeyFor(customerUser.customerId)

const profile: Profile = {
  id: customerUser.customerId,
  email: customerUser.email,
  firstName: 'Casey',
  lastName: 'Sample',
  phone: '+14155550123',
  emailConfirmed: true,
  createdAt: '2026-01-15T09:30:00Z',
}

const withEtag = (body: Profile, etag: string, status = 200) =>
  HttpResponse.json(body, { status, headers: { ETag: etag } })

// Serves GET /customers/me from a variable, so a test can change what the next read returns.
function serveProfile(initial: Profile = profile, etag = '"1"') {
  const state = { profile: initial, etag, gets: 0 }
  server.use(
    http.get('*/api/v1/customers/me', () => {
      state.gets++
      return withEtag(state.profile, state.etag)
    }),
  )
  return state
}

describe('my account', () => {
  it('shows name, email with "contact support", phone and member since [US-F033]', async () => {
    signedInAs(customerUser)
    serveProfile()
    renderApp('/account')

    expect(await screen.findByText('Casey Sample')).toBeVisible()
    expect(within(screen.getByRole('main')).getByText(customerUser.email)).toBeVisible()
    expect(screen.getByText('To change your email, contact support.')).toBeVisible()
    expect(screen.getByText('+14155550123')).toBeVisible()
    expect(screen.getByText(formatDateTime('2026-01-15T09:30:00Z'))).toBeVisible()
    expect(screen.getByRole('link', { name: 'Edit profile' })).toHaveAttribute(
      'href',
      '/account/edit',
    )
    expect(screen.getByRole('link', { name: 'Profile' })).toHaveAttribute('aria-current', 'page')
  })

  it('shows "Not added" when there is no phone', async () => {
    signedInAs(customerUser)
    serveProfile({ ...profile, phone: null })
    renderApp('/account')

    expect(await screen.findByText('Not added')).toBeVisible()
  })

  it('shows a message when the record is gone (404)', async () => {
    signedInAs(customerUser)
    server.use(http.get('*/api/v1/customers/me', () => problem(404, 'not-found')))
    renderApp('/account')

    expect(await screen.findByText('We could not find your account.')).toBeVisible()
  })

  it('shows an error with a retry', async () => {
    signedInAs(customerUser)
    server.use(http.get('*/api/v1/customers/me', () => problem(500, 'internal-error')))
    renderApp('/account')

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong. Try again.')
    serveProfile()
    await userEvent.setup().click(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByText('Casey Sample')).toBeVisible()
  })

  it('shows "not allowed" to an Admin, who has no customer record', async () => {
    signedInAs(adminUser)
    renderApp('/account')

    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: 'You do not have access to this page.',
      }),
    ).toBeVisible()
  })
})

describe('edit profile', () => {
  async function openEdit() {
    const state = serveProfile()
    signedInAs(customerUser)
    const view = renderApp('/account/edit')
    const user = userEvent.setup()
    await screen.findByLabelText('First name')
    return { ...view, user, state }
  }

  it('saves with If-Match from the ETag, shows the saved values, and keeps the new ETag [US-F033 AC1]', async () => {
    const puts: { ifMatch: string | null; body: unknown; csrf: string | null }[] = []
    let etag = '"2"'
    server.use(
      http.put('*/api/v1/customers/me', async ({ request }) => {
        puts.push({
          ifMatch: request.headers.get('If-Match'),
          body: await request.json(),
          csrf: request.headers.get('X-CSRF'),
        })
        const saved = { ...profile, phone: '+14155550999' }
        const response = withEtag(saved, etag)
        etag = '"3"'
        return response
      }),
    )
    const { user } = await openEdit()

    const phone = screen.getByLabelText('Phone (optional)')
    await user.clear(phone)
    await user.type(phone, '+14155550999')
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'My account' })).toBeVisible()
    expect(puts[0]).toEqual({
      ifMatch: '"1"',
      csrf: '1',
      body: { firstName: 'Casey', lastName: 'Sample', phone: '+14155550999' },
    })
    expect(screen.getByText('+14155550999')).toBeVisible()
    expect(screen.getByText('Profile saved.')).toBeVisible()

    // The ETag from the PUT response is the one kept: a second save sends it.
    await user.click(screen.getByRole('link', { name: 'Edit profile' }))
    await screen.findByLabelText('First name')
    await user.click(screen.getByRole('button', { name: 'Save changes' }))
    await waitFor(() => {
      expect(puts).toHaveLength(2)
    })
    expect(puts[1]?.ifMatch).toBe('"2"')
  })

  it('clears the phone when the box is empty [US-F033 AC3]', async () => {
    let body: unknown
    server.use(
      http.put('*/api/v1/customers/me', async ({ request }) => {
        body = await request.json()
        return withEtag({ ...profile, phone: null }, '"2"')
      }),
    )
    const { user } = await openEdit()

    await user.clear(screen.getByLabelText('Phone (optional)'))
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText('Not added')).toBeVisible()
    expect(body).toEqual({ firstName: 'Casey', lastName: 'Sample', phone: null })
  })

  it('shows the email as read-only text with "contact support", not as a field', async () => {
    await openEdit()

    expect(within(screen.getByRole('main')).getByText(customerUser.email)).toBeVisible()
    expect(screen.getByText('To change your email, contact support.')).toBeVisible()
    expect(screen.queryByRole('textbox', { name: /email/i })).not.toBeInTheDocument()
  })

  it('reloads the latest data on 412 and keeps the typed values under each field [US-F033 AC2]', async () => {
    const puts: (string | null)[] = []
    const { user, state } = await openEdit()
    server.use(
      http.put('*/api/v1/customers/me', ({ request }) => {
        puts.push(request.headers.get('If-Match'))
        if (puts.length === 1) {
          // Changed in another tab: new phone and a new ETag.
          state.profile = { ...profile, phone: '+14155550000', lastName: 'Other' }
          state.etag = '"7"'
          return problem(412, 'precondition-failed')
        }
        return withEtag({ ...state.profile, phone: '+14155550999' }, '"8"')
      }),
    )

    const phone = screen.getByLabelText('Phone (optional)')
    await user.clear(phone)
    await user.type(phone, '+14155550999')
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    const alert = await screen.findByText('This record changed since you opened it.')
    // Focus moves to the warning; the form shows the latest saved values.
    await waitFor(() => {
      expect(alert.closest('[role="alert"]')).toHaveFocus()
    })
    expect(screen.getByLabelText('Phone (optional)')).toHaveValue('+14155550000')
    expect(screen.getByLabelText('Last name')).toHaveValue('Other')
    // The typed value is kept and offered; fields that did not differ show nothing.
    expect(screen.getByText('Your change: +14155550999')).toBeVisible()
    expect(screen.queryByText(/Your change: Sample/)).not.toBeInTheDocument()
    expect(puts).toHaveLength(1)

    await user.click(screen.getByRole('button', { name: 'Use my change for phone' }))
    expect(screen.getByLabelText('Phone (optional)')).toHaveValue('+14155550999')
    expect(screen.queryByText('Your change: +14155550999')).not.toBeInTheDocument()

    // Nothing was saved on its own; the second Save sends the new ETag.
    await user.click(screen.getByRole('button', { name: 'Save changes' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'My account' })).toBeVisible()
    expect(puts).toEqual(['"1"', '"7"'])
  })

  it('shows a generic error with the reference for 428 [NFR-F22]', async () => {
    server.use(http.put('*/api/v1/customers/me', () => problem(428, 'precondition-required')))
    const { user } = await openEdit()

    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText('Something went wrong on our side.')).toBeVisible()
    expect(screen.getByText('Reference: trace-test-123')).toBeVisible()
    expect(screen.queryByText('This record changed since you opened it.')).not.toBeInTheDocument()
  })

  it('shows server field errors on the fields and focuses the first', async () => {
    server.use(
      http.put('*/api/v1/customers/me', () =>
        problem(400, 'validation-failed', { errors: { Phone: ['Phone number is not valid.'] } }),
      ),
    )
    const { user } = await openEdit()

    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    const phone = screen.getByLabelText('Phone (optional)')
    await waitFor(() => {
      expect(phone).toHaveAccessibleDescription(/Phone number is not valid\./)
    })
    expect(phone).toHaveFocus()
  })

  it('blocks an invalid phone and an empty name without a request', async () => {
    let puts = 0
    server.use(
      http.put('*/api/v1/customers/me', () => {
        puts++
        return withEtag(profile, '"2"')
      }),
    )
    const { user } = await openEdit()

    await user.clear(screen.getByLabelText('First name'))
    await user.clear(screen.getByLabelText('Phone (optional)'))
    await user.type(screen.getByLabelText('Phone (optional)'), '12345')
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText('Enter your first name.')).toBeVisible()
    expect(screen.getByText(/Enter the phone number with \+ and the country code/)).toBeVisible()
    expect(screen.getByLabelText('First name')).toHaveFocus()
    expect(puts).toBe(0)
  })

  it('shows the 429 countdown and keeps Save off', async () => {
    server.use(
      http.put('*/api/v1/customers/me', () =>
        problem(429, 'rate-limited', {}, { 'Retry-After': '30' }),
      ),
    )
    const { user } = await openEdit()

    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText('Too many attempts. Try again in 30 seconds.')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Save changes' })).toBeDisabled()
  })

  it('does not send twice on a double click', async () => {
    let release: () => void = () => undefined
    const gate = new Promise<void>((resolve) => {
      release = resolve
    })
    let puts = 0
    server.use(
      http.put('*/api/v1/customers/me', async () => {
        puts++
        await gate
        return withEtag(profile, '"2"')
      }),
    )
    const { user } = await openEdit()

    await user.dblClick(screen.getByRole('button', { name: 'Save changes' }))
    expect(await screen.findByRole('button', { name: 'Saving…' })).toBeDisabled()
    release()

    await screen.findByRole('heading', { level: 1, name: 'My account' })
    expect(puts).toBe(1)
  })
})

describe('change password', () => {
  const fill = async (
    current = 'Old-sample-password-1',
    next = 'a brand new sample passphrase',
  ) => {
    const user = userEvent.setup()
    await user.type(await screen.findByLabelText('Current password'), current)
    await user.type(screen.getByLabelText('New password'), next)
    await user.click(screen.getByRole('button', { name: 'Change password' }))
    return user
  }

  it('signs the user out, goes to sign-in with the message, and keeps the cart [US-F034 AC1]', async () => {
    signedInAs(customerUser)
    writeCart(cartKey, [{ productId: productId(1), quantity: 2, price: 11.5 }])
    let body: unknown
    let csrf: string | null = null
    server.use(
      http.post('*/api/v1/auth/change-password', async ({ request }) => {
        csrf = request.headers.get('X-CSRF')
        body = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { queryClient } = renderApp('/account/password')
    // Something cached for this user, to check the cache is dropped.
    queryClient.setQueryData(['profile'], { profile, etag: '"1"' })

    await fill()

    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeVisible()
    expect(screen.getByText('Password changed. Sign in with your new password.')).toBeVisible()
    expect(body).toEqual({
      currentPassword: 'Old-sample-password-1',
      newPassword: 'a brand new sample passphrase',
    })
    expect(csrf).toBe('1')
    expect(queryClient.getQueryData(['profile'])).toBeUndefined()
    // A session that just ends keeps the cart (UX-D2).
    expect(readCart(cartKey)).toHaveLength(1)
    expect(screen.queryByRole('button', { name: 'Sign out' })).not.toBeInTheDocument()
  })

  it('says all devices are signed out', async () => {
    signedInAs(customerUser)
    renderApp('/account/password')

    expect(
      await screen.findByText('After you change it, you will be signed out on all devices.'),
    ).toBeVisible()
  })

  it('shows the error on the current password with the lockout warning [US-F034 AC2]', async () => {
    signedInAs(customerUser)
    server.use(
      http.post('*/api/v1/auth/change-password', () =>
        problem(400, 'validation-failed', {
          errors: { currentPassword: ['Incorrect password.'] },
        }),
      ),
    )
    renderApp('/account/password')

    await fill()

    const current = screen.getByLabelText('Current password')
    await waitFor(() => {
      expect(current).toHaveAccessibleDescription(
        'Current password is incorrect. After 5 failed attempts your account is locked for 15 minutes.',
      )
    })
    expect(current).toHaveFocus()
    // Still signed in, on the same page.
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeVisible()
  })

  it('blocks a short new password and an empty current one without a request', async () => {
    signedInAs(customerUser)
    let posts = 0
    server.use(
      http.post('*/api/v1/auth/change-password', () => {
        posts++
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderApp('/account/password')
    const user = userEvent.setup()
    await user.type(await screen.findByLabelText('New password'), 'too short')
    await user.click(screen.getByRole('button', { name: 'Change password' }))

    expect(await screen.findByText('Enter your current password.')).toBeVisible()
    expect(screen.getByText(/Use at least 15 characters/)).toBeVisible()
    expect(posts).toBe(0)
  })

  it('shows the blocklist message from the API on the new password', async () => {
    signedInAs(customerUser)
    server.use(
      http.post('*/api/v1/auth/change-password', () =>
        problem(400, 'validation-failed', {
          errors: { newPassword: ['This password is too common.'] },
        }),
      ),
    )
    renderApp('/account/password')

    await fill()

    expect(await screen.findByText('This password is too common.')).toBeVisible()
    expect(screen.getByLabelText('New password')).toHaveFocus()
  })

  it('shows the 429 countdown and keeps the button off', async () => {
    signedInAs(customerUser)
    server.use(
      http.post('*/api/v1/auth/change-password', () =>
        problem(429, 'rate-limited', {}, { 'Retry-After': '30' }),
      ),
    )
    renderApp('/account/password')

    await fill()

    expect(await screen.findByText('Too many attempts. Try again in 30 seconds.')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Change password' })).toBeDisabled()
  })
})

describe('delete account', () => {
  async function start(password = 'Old-sample-password-1') {
    const user = userEvent.setup()
    await user.type(await screen.findByLabelText('Current password'), password)
    await user.click(screen.getByLabelText('I understand this cannot be undone.'))
    await user.click(screen.getByRole('button', { name: 'Delete my account' }))
    return user
  }

  it('explains what is erased and what is kept [NFR-F17]', async () => {
    signedInAs(customerUser)
    renderApp('/account/delete')

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Delete your account' }),
    ).toBeVisible()
    expect(screen.getByText('Your name, email and phone are erased.')).toBeVisible()
    expect(
      screen.getByText('Your order records are kept without your personal data, for accounting.'),
    ).toBeVisible()
    expect(screen.getByText('Open orders must be cancelled or finished first.')).toBeVisible()
  })

  it('asks twice, then erases, ends the session, clears the cart and says so [US-F035 AC1]', async () => {
    signedInAs(customerUser)
    writeCart(cartKey, [{ productId: productId(1), quantity: 2, price: 11.5 }])
    let posts = 0
    let body: unknown
    server.use(
      http.post('*/api/v1/customers/me/erase', async ({ request }) => {
        posts++
        body = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderApp('/account/delete')

    const user = await start()

    // First confirmation done; nothing is sent until the dialog is confirmed.
    const dialog = await screen.findByRole('alertdialog', {
      name: 'Delete your account permanently?',
    })
    expect(posts).toBe(0)
    // Focus starts on the safe button.
    expect(within(dialog).getByRole('button', { name: 'Keep my account' })).toHaveFocus()

    await user.click(within(dialog).getByRole('button', { name: 'Delete account' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeVisible()
    expect(screen.getByText('Your account was erased.')).toBeVisible()
    expect(body).toEqual({ currentPassword: 'Old-sample-password-1' })
    expect(posts).toBe(1)
    expect(readCart(cartKey)).toEqual([])
  })

  it('does nothing when the dialog is dismissed with "Keep my account"', async () => {
    signedInAs(customerUser)
    let posts = 0
    server.use(
      http.post('*/api/v1/customers/me/erase', () => {
        posts++
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderApp('/account/delete')
    const user = await start()
    const dialog = await screen.findByRole('alertdialog')

    await user.click(within(dialog).getByRole('button', { name: 'Keep my account' }))

    await waitFor(() => {
      expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    })
    expect(posts).toBe(0)
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeVisible()
  })

  it('needs the password and the box before the dialog opens', async () => {
    signedInAs(customerUser)
    renderApp('/account/delete')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Delete my account' }))

    expect(await screen.findByText('Enter your current password.')).toBeVisible()
    expect(screen.getByText('Tick the box to continue.')).toBeVisible()
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
  })

  it('links to My orders when there are open orders (409) and keeps the session [US-F035 AC2]', async () => {
    signedInAs(customerUser)
    writeCart(cartKey, [{ productId: productId(1), quantity: 2, price: 11.5 }])
    server.use(
      http.post('*/api/v1/customers/me/erase', () => problem(409, 'customer-has-open-orders')),
    )
    renderApp('/account/delete')
    const user = await start()
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', {
        name: 'Delete account',
      }),
    )

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Cancel or finish your open orders first.')
    expect(within(alert).getByRole('link', { name: 'Go to My orders' })).toHaveAttribute(
      'href',
      '/orders',
    )
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeVisible()
    expect(readCart(cartKey)).toHaveLength(1)
  })

  it('shows a wrong password on the password field and focuses it [US-F035 AC3]', async () => {
    signedInAs(customerUser)
    server.use(
      http.post('*/api/v1/customers/me/erase', () =>
        problem(400, 'validation-failed', {
          errors: { currentPassword: ['Incorrect password.'] },
        }),
      ),
    )
    renderApp('/account/delete')
    const user = await start()
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', {
        name: 'Delete account',
      }),
    )

    const password = screen.getByLabelText('Current password')
    await waitFor(() => {
      expect(password).toHaveAccessibleDescription(/Incorrect password\./)
    })
    await waitFor(() => {
      expect(password).toHaveFocus()
    })
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
  })

  it('shows the 429 countdown and keeps the button off', async () => {
    signedInAs(customerUser)
    server.use(
      http.post('*/api/v1/customers/me/erase', () =>
        problem(429, 'rate-limited', {}, { 'Retry-After': '30' }),
      ),
    )
    renderApp('/account/delete')
    const user = await start()
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', {
        name: 'Delete account',
      }),
    )

    expect(await screen.findByText('Too many attempts. Try again in 30 seconds.')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Delete my account' })).toBeDisabled()
  })

  it('does not send twice from the dialog', async () => {
    signedInAs(customerUser)
    let release: () => void = () => undefined
    const gate = new Promise<void>((resolve) => {
      release = resolve
    })
    let posts = 0
    server.use(
      http.post('*/api/v1/customers/me/erase', async () => {
        posts++
        await gate
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderApp('/account/delete')
    const user = await start()
    const dialog = await screen.findByRole('alertdialog')

    await user.dblClick(within(dialog).getByRole('button', { name: 'Delete account' }))
    expect(await within(dialog).findByRole('button', { name: 'Deleting…' })).toBeDisabled()
    release()

    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    expect(posts).toBe(1)
  })
})

describe('sign out everywhere', () => {
  it('calls the BFF, ends the session and clears the cart on this device [US-F036 AC1]', async () => {
    signedInAs(customerUser)
    serveProfile()
    writeCart(cartKey, [{ productId: productId(1), quantity: 2, price: 11.5 }])
    let posts = 0
    let csrf: string | null = null
    server.use(
      http.post('*/bff/logout-all', ({ request }) => {
        posts++
        csrf = request.headers.get('X-CSRF')
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderApp('/account')

    await userEvent
      .setup()
      .click(await screen.findByRole('button', { name: 'Sign out everywhere' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeVisible()
    expect(posts).toBe(1)
    expect(csrf).toBe('1')
    expect(readCart(cartKey)).toEqual([])
  })

  it('still leaves the signed-in state when the call fails', async () => {
    signedInAs(customerUser)
    serveProfile()
    server.use(http.post('*/bff/logout-all', () => problem(500, 'internal-error')))
    renderApp('/account')

    await userEvent
      .setup()
      .click(await screen.findByRole('button', { name: 'Sign out everywhere' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeVisible()
  })
})
