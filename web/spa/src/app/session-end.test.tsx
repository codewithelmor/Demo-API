import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { profileKey } from '@/features/profile/profile-api'
import { adminUser, customerUser } from '@/test/data'
import { problem } from '@/test/problem'
import { renderApp } from '@/test/render-app'
import { server } from '@/test/server'
import { page, signedInAs } from '@/test/session'

const profileOf = (firstName: string, email: string) => ({
  id: customerUser.customerId,
  email,
  firstName,
  lastName: 'Sample',
  phone: null,
  emailConfirmed: true,
  createdAt: '2026-01-15T09:30:00Z',
})

const otherCustomer = {
  ...customerUser,
  email: 'customer.two@example.test',
  customerId: '5b1b7c9e-0000-4000-8000-000000000002',
}

describe('an involuntary session end (401)', () => {
  it('drops all cached data, says the session ended, and shows nothing of the old user to the next one [NFR-F33]', async () => {
    signedInAs(customerUser)
    let profileCalls = 0
    let expired = false
    let signedInUser = 'A'
    server.use(
      http.get('*/api/v1/customers/me', () => {
        profileCalls++
        if (expired) return problem(401, 'unauthorized')
        return HttpResponse.json(
          signedInUser === 'A'
            ? profileOf('Alice', customerUser.email)
            : profileOf('Bob', otherCustomer.email),
          { headers: { ETag: '"1"' } },
        )
      }),
      http.post('*/bff/login', () => {
        signedInUser = 'B'
        return HttpResponse.json(otherCustomer)
      }),
    )
    const { queryClient, router } = renderApp('/account')
    expect(await screen.findByText('Alice Sample')).toBeVisible()
    expect(queryClient.getQueryData(profileKey)).toBeDefined()

    // The session ends while the tab is open: the next call is a 401.
    expired = true
    void queryClient.invalidateQueries({ queryKey: profileKey })

    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeVisible()
    expect(screen.getByText('Your session ended. Sign in again to continue.')).toBeVisible()
    expect(router.state.location.search).toContain('reason=session-ended')
    expect(router.state.location.search).toContain('returnUrl=%2Faccount')
    // Only the session bookkeeping is left in the cache.
    const keys = queryClient
      .getQueryCache()
      .getAll()
      .map((query) => String(query.queryKey[0]))
    expect(keys.sort()).toEqual(['session', 'session-end'])
    // The failed call was the last one: clearing the cache started no refetch.
    const callsAtEnd = profileCalls
    await new Promise((resolve) => setTimeout(resolve, 150))
    expect(profileCalls).toBe(callsAtEnd)

    // Another customer signs in in the same tab (no reload) and is sent back to /account.
    expired = false
    const user = userEvent.setup()
    await user.type(screen.getByLabelText('Email'), otherCustomer.email)
    await user.type(screen.getByLabelText('Password'), 'Sample-Pass-123!')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Bob Sample')).toBeVisible()
    expect(screen.queryByText('Alice Sample')).not.toBeInTheDocument()
  })

  it('also drops order data before the next sign-in', async () => {
    signedInAs(customerUser)
    server.use(http.get('*/api/v1/customers/me/orders', () => page([])))
    const { queryClient } = renderApp('/orders')
    await screen.findByText('You have not placed any orders yet.')
    expect(queryClient.getQueryCache().getAll().length).toBeGreaterThan(1)

    server.use(http.get('*/api/v1/customers/me/orders', () => problem(401, 'unauthorized')))
    void queryClient.invalidateQueries({ queryKey: ['orders'] })

    await waitFor(() => {
      expect(
        queryClient
          .getQueryCache()
          .getAll()
          .map((query) => String(query.queryKey[0]))
          .sort(),
      ).toEqual(['session', 'session-end'])
    })
  })
})

describe('guarded loaders make no API call for the wrong role', () => {
  function countRequests(pattern: string) {
    const urls: string[] = []
    server.use(
      http.get(pattern, ({ request }) => {
        urls.push(request.url)
        return page([])
      }),
    )
    return urls
  }

  it('sends an anonymous visitor to sign-in without calling the orders or profile API', async () => {
    server.use(http.get('*/bff/user', () => problem(401, 'unauthorized')))
    const orders = countRequests('*/api/v1/customers/me/orders')
    const profile = countRequests('*/api/v1/customers/me')
    const products = countRequests('*/api/v1/products')

    renderApp('/orders')
    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeVisible()
    await new Promise((resolve) => setTimeout(resolve, 100))

    expect(orders).toEqual([])
    expect(profile).toEqual([])
    expect(products).toEqual([])
  })

  it.each(['/orders', '/orders/11111111-0000-4000-8000-000000000001', '/account', '/account/edit'])(
    'shows "not allowed" to an Admin on %s without calling the API',
    async (path) => {
      signedInAs(adminUser)
      const orders = countRequests('*/api/v1/customers/me/orders*')
      const profile = countRequests('*/api/v1/customers/me')

      renderApp(path)
      expect(
        await screen.findByRole('heading', {
          level: 1,
          name: 'You do not have access to this page.',
        }),
      ).toBeVisible()
      await new Promise((resolve) => setTimeout(resolve, 100))

      expect(orders).toEqual([])
      expect(profile).toEqual([])
    },
  )
})
