import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http } from 'msw'
import { describe, expect, it } from 'vitest'
import type { MyOrderListItem } from '@/api/orders'
import { formatDateTime, localDayStartIso } from '@/lib/dates'
import { adminUser, customerUser } from '@/test/data'
import { problem } from '@/test/problem'
import { renderApp } from '@/test/render-app'
import { server } from '@/test/server'
import { page, signedInAs } from '@/test/session'

const item = (n: number, overrides: Partial<MyOrderListItem> = {}): MyOrderListItem => ({
  id: `${String(n).padStart(8, '0')}-0000-4000-8000-000000000001`,
  status: 'Pending',
  total: 10 * n,
  currency: 'USD',
  createdAt: '2026-03-10T09:30:00Z',
  ...overrides,
})

function recordList(respond: (query: URLSearchParams) => Response = () => page([item(1)])) {
  const queries: URLSearchParams[] = []
  server.use(
    http.get('*/api/v1/customers/me/orders', ({ request }) => {
      const query = new URL(request.url).searchParams
      queries.push(query)
      return respond(query)
    }),
  )
  return queries
}

describe('my orders', () => {
  it('lists orders newest first with status, total and the date in my time zone [US-F030 AC1]', async () => {
    signedInAs(customerUser)
    const queries = recordList(() =>
      page([
        item(2, { status: 'Shipped', total: 20, createdAt: '2026-03-12T08:00:00Z' }),
        item(1, { status: 'Cancelled', total: 1234.5 }),
      ]),
    )
    renderApp('/orders')

    const table = await screen.findByRole('table', { name: 'Your orders' })
    const rows = within(table).getAllByRole('row').slice(1)
    expect(rows).toHaveLength(2)
    const [first, second] = rows
    if (!first || !second) throw new Error('Expected two rows.')
    expect(within(first).getByRole('link', { name: 'Order 00000002' })).toHaveAttribute(
      'href',
      '/orders/00000002-0000-4000-8000-000000000001',
    )
    expect(within(first).getByText('Shipped')).toBeVisible()
    expect(within(first).getByText('$20.00')).toBeVisible()
    // The same formatter the page uses: the viewer's own time zone and locale.
    expect(within(first).getByText(formatDateTime('2026-03-12T08:00:00Z'))).toBeVisible()
    expect(within(second).getByText('$1,234.50')).toBeVisible()
    expect(within(second).getByText('Cancelled')).toBeVisible()
    expect(queries[0]?.get('sort')).toBe('-createdAt')
    expect(queries[0]?.get('pageSize')).toBe('20')
  })

  it('filters by several statuses, in the URL and in the request [US-F030]', async () => {
    signedInAs(customerUser)
    const queries = recordList()
    const { router } = renderApp('/orders')
    const user = userEvent.setup()
    await screen.findByRole('table')

    await user.click(screen.getByLabelText('Pending'))
    await user.click(screen.getByLabelText('Shipped'))
    await user.click(screen.getByRole('button', { name: 'Apply filters' }))

    await waitFor(() => {
      expect(queries.at(-1)?.getAll('status')).toEqual(['Pending', 'Shipped'])
    })
    expect(router.state.location.search).toBe('?status=Pending&status=Shipped')
    expect(screen.getByLabelText('Pending')).toBeChecked()
  })

  it('sends the date range as whole days in my time zone [US-F030]', async () => {
    signedInAs(customerUser)
    const queries = recordList()
    const { router } = renderApp('/orders?page=2')
    const user = userEvent.setup()
    await screen.findByRole('table')

    await user.type(screen.getByLabelText('From'), '2026-03-01')
    await user.type(screen.getByLabelText('To'), '2026-03-10')
    await user.click(screen.getByRole('button', { name: 'Apply filters' }))

    await waitFor(() => {
      expect(queries.at(-1)?.get('createdFrom')).toBe(localDayStartIso('2026-03-01'))
    })
    // To is inclusive for the user, exclusive in the API: the start of the next day.
    expect(queries.at(-1)?.get('createdTo')).toBe(localDayStartIso('2026-03-10', 1))
    expect(queries.at(-1)?.get('page')).toBe('1')
    expect(router.state.location.search).toBe('?from=2026-03-01&to=2026-03-10')
  })

  it('shows a field error and makes no call when To is before From [US-F030 AC2]', async () => {
    signedInAs(customerUser)
    const queries = recordList()
    renderApp('/orders')
    const user = userEvent.setup()
    await screen.findByRole('table')
    const callsBefore = queries.length

    await user.type(screen.getByLabelText('From'), '2026-03-10')
    await user.type(screen.getByLabelText('To'), '2026-03-01')
    await user.click(screen.getByRole('button', { name: 'Apply filters' }))

    const to = screen.getByLabelText('To')
    await waitFor(() => {
      expect(to).toHaveAccessibleDescription('The To date must be on or after the From date.')
    })
    expect(to).toHaveFocus()
    expect(queries).toHaveLength(callsBefore)
  })

  it('sorts by total and goes back to page 1', async () => {
    signedInAs(customerUser)
    const queries = recordList()
    const { router } = renderApp('/orders?page=2')
    const user = userEvent.setup()
    await screen.findByRole('table')

    await user.selectOptions(screen.getByLabelText('Sort'), 'Total high to low')

    await waitFor(() => {
      expect(queries.at(-1)?.get('sort')).toBe('-total')
    })
    expect(router.state.location.search).toBe('?sort=-total')
  })

  it('reads everything from the URL and ignores bad values', async () => {
    signedInAs(customerUser)
    const queries = recordList(() => page([item(1)], 45, 2))
    renderApp('/orders?status=Shipped&status=Bogus&from=2026-03-01&to=2026-02-01&sort=total&page=2')

    await screen.findByText('Page 2 of 3')

    const query = queries[0]
    expect(query?.getAll('status')).toEqual(['Shipped'])
    expect(query?.get('createdFrom')).toBe(localDayStartIso('2026-03-01'))
    // To before From in the URL is dropped.
    expect(query?.has('createdTo')).toBe(false)
    expect(query?.get('sort')).toBe('total')
    expect(query?.get('page')).toBe('2')
  })

  it('shows an empty state with a link to the catalog [US-F030 AC3]', async () => {
    signedInAs(customerUser)
    recordList(() => page([]))
    renderApp('/orders')

    expect(await screen.findByText('You have not placed any orders yet.')).toBeVisible()
    expect(screen.getByRole('link', { name: 'Browse products' })).toHaveAttribute(
      'href',
      '/products',
    )
  })

  it('offers "Clear filters" when nothing matches', async () => {
    signedInAs(customerUser)
    recordList((query) => (query.has('status') ? page([]) : page([item(1)])))
    const { router } = renderApp('/orders?status=Completed')
    const user = userEvent.setup()

    expect(await screen.findByText('No orders match these filters.')).toBeVisible()
    await user.click(screen.getAllByRole('button', { name: 'Clear filters' })[0] as HTMLElement)

    expect(await screen.findByRole('table')).toBeVisible()
    expect(router.state.location.search).toBe('')
  })

  it('keeps the filters when paging', async () => {
    signedInAs(customerUser)
    recordList(() => page([item(1)], 45))
    renderApp('/orders?status=Pending&sort=total')

    expect(await screen.findByRole('link', { name: 'Next' })).toHaveAttribute(
      'href',
      '/orders?status=Pending&sort=total&page=2',
    )
  })

  it('shows an error with a retry', async () => {
    signedInAs(customerUser)
    server.use(http.get('*/api/v1/customers/me/orders', () => problem(500, 'internal-error')))
    renderApp('/orders')

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong. Try again.')
    server.use(http.get('*/api/v1/customers/me/orders', () => page([item(1)])))
    await userEvent.setup().click(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByRole('table')).toBeVisible()
  })

  it('shows a loading state first', async () => {
    signedInAs(customerUser)
    let release: () => void = () => undefined
    const gate = new Promise<void>((resolve) => {
      release = resolve
    })
    server.use(
      http.get('*/api/v1/customers/me/orders', async () => {
        await gate
        return page([item(1)])
      }),
    )
    renderApp('/orders')

    expect(await screen.findByText('Loading orders…')).toBeInTheDocument()
    release()
    expect(await screen.findByRole('table')).toBeVisible()
  })

  it('is linked from the header for a Customer', async () => {
    signedInAs(customerUser)
    recordList()
    renderApp('/orders')
    expect(await screen.findByRole('link', { name: 'My orders' })).toBeVisible()
  })

  it('shows "not allowed" to an Admin', async () => {
    signedInAs(adminUser)
    renderApp('/orders')
    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: 'You do not have access to this page.',
      }),
    ).toBeVisible()
  })
})
