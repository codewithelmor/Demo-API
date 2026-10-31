import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { adminUser, customerUser, makeProduct } from '@/test/data'
import { problem } from '@/test/problem'
import { renderApp } from '@/test/render-app'
import { server } from '@/test/server'

function signedInAs(user: typeof customerUser) {
  server.use(http.get('*/bff/user', () => HttpResponse.json(user)))
}

describe('catalog list', () => {
  it('lists products through the typed client [US-F001 AC1]', async () => {
    signedInAs(customerUser)
    let query: URLSearchParams | undefined
    server.use(
      http.get('*/api/v1/products', ({ request }) => {
        query = new URL(request.url).searchParams
        return HttpResponse.json({
          items: [makeProduct(1), makeProduct(2)],
          page: 1,
          pageSize: 12,
          totalCount: 2,
          totalPages: 1,
        })
      }),
    )
    renderApp('/products')

    const list = await within(await screen.findByRole('main')).findByRole('list')
    const items = within(list).getAllByRole('listitem')
    expect(items).toHaveLength(2)
    const [first, second] = items
    if (!first || !second) throw new Error('Expected two products.')
    expect(within(first).getByRole('heading', { name: 'Sample product 1' })).toBeInTheDocument()
    expect(within(first).getByText('$11.50')).toBeInTheDocument()
    expect(within(first).getByText('In stock')).toBeInTheDocument()
    // The badge and the disabled button both say it.
    expect(within(second).getAllByText('Out of stock')).toHaveLength(2)
    expect(screen.getByText('2 products')).toBeInTheDocument()
    expect(query?.get('page')).toBe('1')
    expect(query?.get('pageSize')).toBe('20')
    expect(query?.get('sort')).toBe('name')
  })

  it('pages with Next', async () => {
    signedInAs(customerUser)
    server.use(
      http.get('*/api/v1/products', ({ request }) => {
        const page = Number(new URL(request.url).searchParams.get('page'))
        return HttpResponse.json({
          items: [makeProduct(page)],
          page,
          pageSize: 12,
          totalCount: 24,
          totalPages: 2,
        })
      }),
    )
    renderApp('/products')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('link', { name: 'Next' }))

    expect(await screen.findByText('Sample product 2')).toBeInTheDocument()
    expect(screen.getByText('Page 2 of 2')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Next' })).not.toBeInTheDocument()
  })

  it('shows an empty state', async () => {
    signedInAs(customerUser)
    server.use(
      http.get('*/api/v1/products', () =>
        HttpResponse.json({ items: [], page: 1, pageSize: 12, totalCount: 0, totalPages: 0 }),
      ),
    )
    renderApp('/products')

    expect(await screen.findByText('No products yet. Check back later.')).toBeInTheDocument()
  })

  it('shows the traceId reference and a retry on a server error [US-F007 AC3]', async () => {
    signedInAs(customerUser)
    server.use(http.get('*/api/v1/products', () => problem(500, 'internal-error')))
    renderApp('/products')

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Something went wrong. Try again. Reference: trace-test-123')

    server.use(
      http.get('*/api/v1/products', () =>
        HttpResponse.json({
          items: [makeProduct(1)],
          page: 1,
          pageSize: 12,
          totalCount: 1,
          totalPages: 1,
        }),
      ),
    )
    await userEvent.setup().click(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByText('Sample product 1')).toBeInTheDocument()
  })

  it('goes to sign-in when the session ended during the call', async () => {
    signedInAs(customerUser)
    server.use(http.get('*/api/v1/products', () => problem(401, 'session-ended')))
    const { router } = renderApp('/products')

    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeInTheDocument()
    expect(screen.getByText('Your session ended. Sign in again to continue.')).toBeVisible()
    expect(router.state.location.search).toBe('?reason=session-ended&returnUrl=%2Fproducts')
  })

  it('signs out, clears the session and returns to sign-in [US-F004 AC1]', async () => {
    signedInAs(customerUser)
    let csrf: string | null = null
    server.use(
      http.post('*/bff/logout', ({ request }) => {
        csrf = request.headers.get('X-CSRF')
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderApp('/products')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Sign out' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeInTheDocument()
    expect(screen.getByText('You signed out.')).toBeInTheDocument()
    expect(csrf).toBe('1')
    expect(screen.queryByRole('button', { name: 'Sign out' })).not.toBeInTheDocument()
  })

  it('signs out locally even when the BFF call fails [US-F004 AC2]', async () => {
    signedInAs(customerUser)
    server.use(http.post('*/bff/logout', () => problem(503, 'upstream-unavailable')))
    renderApp('/products')

    await userEvent.setup().click(await screen.findByRole('button', { name: 'Sign out' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeInTheDocument()
  })
})

describe('role guard', () => {
  it('shows "not allowed" to a Customer on /admin and loads no admin page [US-F006 AC2]', async () => {
    signedInAs(customerUser)
    renderApp('/admin')

    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: 'You do not have access to this page.',
      }),
    ).toBeInTheDocument()
    expect(screen.queryByRole('heading', { level: 1, name: 'Back office' })).not.toBeInTheDocument()
  })

  it('shows the back office to an Admin [US-F006 AC1]', async () => {
    signedInAs(adminUser)
    renderApp('/admin')

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Back office' }),
    ).toBeInTheDocument()
  })

  it('redirects "/" to the role home', async () => {
    signedInAs(customerUser)
    renderApp('/')
    expect(await screen.findByRole('heading', { level: 1, name: 'Products' })).toBeInTheDocument()
  })

  it('shows the not found page for unknown routes', async () => {
    signedInAs(customerUser)
    renderApp('/nowhere')
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Page not found' }),
    ).toBeInTheDocument()
  })

  it('shows the unexpected-error page with a reference when the session call fails', async () => {
    server.use(http.get('*/bff/user', () => problem(503, 'upstream-unavailable')))
    renderApp('/products')

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Something went wrong' }),
    ).toBeInTheDocument()
    expect(screen.getByText('trace-test-123')).toBeInTheDocument()
  })
})
