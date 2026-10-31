import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { MyOrder } from '@/api/orders'
import { formatDateTime } from '@/lib/dates'
import { customerUser, makeOrder, makeProduct, productId } from '@/test/data'
import { problem } from '@/test/problem'
import { renderApp } from '@/test/render-app'
import { server } from '@/test/server'
import { serveProducts, signedInAs } from '@/test/session'

const order = makeOrder({
  items: [
    { productId: productId(1), quantity: 2, unitPrice: 11.5, lineTotal: 23 },
    { productId: productId(2), quantity: 1, unitPrice: 12.5, lineTotal: 12.5 },
  ],
  total: 35.5,
})
const url = `/orders/${order.id}`

// The order is served from this variable, so a cancel can change what the next read returns.
function serveOrder(initial: MyOrder) {
  const state = { order: initial, gets: 0 }
  server.use(
    http.get('*/api/v1/customers/me/orders/:orderId', () => {
      state.gets++
      return HttpResponse.json(state.order)
    }),
  )
  return state
}

async function openCancelDialog() {
  const user = userEvent.setup()
  await user.click(await screen.findByRole('button', { name: 'Cancel order' }))
  const dialog = await screen.findByRole('alertdialog', { name: 'Cancel this order?' })
  return { user, dialog }
}

describe('order detail', () => {
  it('shows items with names, prices, line totals, the total and the history [US-F031 AC1]', async () => {
    signedInAs(customerUser)
    serveProducts({ [productId(1)]: makeProduct(1), [productId(2)]: makeProduct(2) })
    serveOrder(
      makeOrder({
        ...order,
        status: 'Confirmed',
        history: [
          { from: null, to: 'Pending', at: '2026-03-10T09:30:00Z', reason: null },
          { from: 'Pending', to: 'Confirmed', at: '2026-03-11T10:00:00Z', reason: 'Paid' },
        ],
      }),
    )
    renderApp(url)

    expect(await screen.findByRole('heading', { level: 1, name: 'Order #11111111' })).toBeVisible()
    const table = screen.getByRole('table', { name: 'Items in this order' })
    expect(await within(table).findByRole('link', { name: 'Sample product 1' })).toBeVisible()
    expect(await within(table).findByRole('link', { name: 'Sample product 2' })).toBeVisible()
    const rows = within(table).getAllByRole('row').slice(1)
    expect(rows[0]).toHaveTextContent('$11.50')
    expect(rows[0]).toHaveTextContent('2')
    expect(rows[0]).toHaveTextContent('$23.00')
    expect(screen.getByText('Total $35.50 (USD)')).toBeVisible()

    // Newest first.
    const items = within(screen.getByRole('list', { name: 'Status history' })).getAllByRole(
      'listitem',
    )
    expect(items[0]).toHaveTextContent('Pending to Confirmed')
    expect(items[0]).toHaveTextContent(formatDateTime('2026-03-11T10:00:00Z'))
    expect(items[0]).toHaveTextContent('Reason: Paid')
    expect(items[1]).toHaveTextContent('Placed as Pending')
    // Not Pending: no Cancel button.
    expect(screen.queryByRole('button', { name: 'Cancel order' })).not.toBeInTheDocument()
    expect(screen.getByText('Only pending orders can be cancelled.')).toBeVisible()
  })

  it('shows "Product no longer available" with the prices when the name lookup is 404 [US-F031 AC2]', async () => {
    signedInAs(customerUser)
    serveProducts({ [productId(1)]: makeProduct(1) }) // product 2 is deleted
    serveOrder(order)
    renderApp(url)

    const missing = await screen.findByText('Product no longer available')
    const row = missing.closest('tr')
    expect(row).toHaveTextContent('$12.50')
    expect(row).toHaveTextContent('1')
    expect(await screen.findByRole('link', { name: 'Sample product 1' })).toBeVisible()
  })

  it('asks for each product once, up to 50 lines', async () => {
    signedInAs(customerUser)
    const items = Array.from({ length: 50 }, (_, i) => ({
      productId: productId(i + 1),
      quantity: 1,
      unitPrice: 1,
      lineTotal: 1,
    }))
    serveOrder(makeOrder({ items, total: 50 }))
    const asked: string[] = []
    server.use(
      http.get('*/api/v1/products/:productId', ({ params }) => {
        asked.push(String(params.productId))
        return HttpResponse.json(makeProduct(Number(String(params.productId).slice(-12))))
      }),
    )
    renderApp(url)

    await waitFor(() => {
      expect(asked).toHaveLength(50)
    })
    expect(new Set(asked).size).toBe(50)
  })

  it('shows "Order not found" for another customer\'s order (404) [US-F031 AC3]', async () => {
    signedInAs(customerUser)
    server.use(http.get('*/api/v1/customers/me/orders/:orderId', () => problem(404, 'not-found')))
    renderApp(url)

    expect(await screen.findByRole('heading', { level: 1, name: 'Order not found' })).toBeVisible()
    expect(screen.getByText('Order not found.')).toBeVisible()
    expect(screen.getByRole('link', { name: 'Go to My orders' })).toHaveAttribute('href', '/orders')
  })

  it('shows an error with a retry', async () => {
    signedInAs(customerUser)
    server.use(
      http.get('*/api/v1/customers/me/orders/:orderId', () => problem(500, 'internal-error')),
    )
    renderApp(url)

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong. Try again.')
    expect(screen.getByRole('button', { name: 'Try again' })).toBeVisible()
  })
})

describe('cancel own order', () => {
  it('cancels a Pending order with a reason, then shows Cancelled and a new history row [US-F032 AC1]', async () => {
    signedInAs(customerUser)
    serveProducts({ [productId(1)]: makeProduct(1), [productId(2)]: makeProduct(2) })
    const state = serveOrder(order)
    let body: unknown
    let csrf: string | null = null
    server.use(
      http.post('*/api/v1/customers/me/orders/:orderId/cancel', async ({ request }) => {
        csrf = request.headers.get('X-CSRF')
        body = await request.json()
        state.order = makeOrder({
          ...order,
          status: 'Cancelled',
          history: [
            ...order.history,
            {
              from: 'Pending',
              to: 'Cancelled',
              at: '2026-03-10T10:00:00Z',
              reason: 'Changed my mind',
            },
          ],
        })
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderApp(url)
    const { user, dialog } = await openCancelDialog()

    expect(
      within(dialog).getByText('The items go back into stock. You cannot undo this.'),
    ).toBeVisible()
    await user.type(within(dialog).getByLabelText('Reason (optional)'), 'Changed my mind')
    expect(within(dialog).getByText('15/500 characters')).toBeVisible()
    await user.click(within(dialog).getByRole('button', { name: 'Cancel order' }))

    await waitFor(() => {
      expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    })
    expect(body).toEqual({ reason: 'Changed my mind' })
    expect(csrf).toBe('1')
    expect(screen.getByText('Cancelled', { selector: 'span' })).toBeVisible()
    expect(screen.getByText(/Pending to Cancelled/)).toBeVisible()
    expect(screen.getByText(/Reason: Changed my mind/)).toBeVisible()
    expect(screen.queryByRole('button', { name: 'Cancel order' })).not.toBeInTheDocument()
    // The change is announced in the live region, and focus is not lost with the Cancel button.
    expect(screen.getByText('Order cancelled.')).toBeVisible()
    await waitFor(() => {
      expect(screen.getByRole('heading', { level: 1, name: 'Order #11111111' })).toHaveFocus()
    })
  })

  it('sends no body when no reason is given, and "Keep order" does nothing [US-F032]', async () => {
    signedInAs(customerUser)
    serveProducts({ [productId(1)]: makeProduct(1), [productId(2)]: makeProduct(2) })
    const state = serveOrder(order)
    let posts = 0
    let text: string | undefined
    server.use(
      http.post('*/api/v1/customers/me/orders/:orderId/cancel', async ({ request }) => {
        posts++
        text = await request.text()
        state.order = makeOrder({ ...order, status: 'Cancelled' })
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderApp(url)
    const first = await openCancelDialog()

    await first.user.click(within(first.dialog).getByRole('button', { name: 'Keep order' }))
    await waitFor(() => {
      expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    })
    expect(posts).toBe(0)

    const second = await openCancelDialog()
    await second.user.click(within(second.dialog).getByRole('button', { name: 'Cancel order' }))
    await waitFor(() => {
      expect(posts).toBe(1)
    })
    expect(text).toBe('')
  })

  it('flags a reason over 500 characters and blocks sending [CU-6]', async () => {
    signedInAs(customerUser)
    serveProducts({ [productId(1)]: makeProduct(1), [productId(2)]: makeProduct(2) })
    serveOrder(order)
    renderApp(url)
    const { user, dialog } = await openCancelDialog()

    await user.click(within(dialog).getByLabelText('Reason (optional)'))
    await user.paste('x'.repeat(600))

    expect(within(dialog).getByText('600/500 characters')).toBeVisible()
    expect(within(dialog).getByLabelText('Reason (optional)')).toHaveAccessibleDescription(
      /Use 500 characters or fewer/,
    )
    expect(within(dialog).getByRole('button', { name: 'Cancel order' })).toBeDisabled()
  })

  it('shows the current status and reloads when the order was confirmed meanwhile [US-F032 AC2]', async () => {
    signedInAs(customerUser)
    serveProducts({ [productId(1)]: makeProduct(1), [productId(2)]: makeProduct(2) })
    const state = serveOrder(order)
    server.use(
      http.post('*/api/v1/customers/me/orders/:orderId/cancel', () => {
        state.order = makeOrder({ ...order, status: 'Confirmed' })
        return problem(409, 'invalid-status-transition', { currentStatus: 'Confirmed' })
      }),
    )
    renderApp(url)
    const { user, dialog } = await openCancelDialog()
    const getsBefore = state.gets

    await user.click(within(dialog).getByRole('button', { name: 'Cancel order' }))

    expect(
      await screen.findByText('This order can no longer be cancelled (status: Confirmed).'),
    ).toBeVisible()
    await waitFor(() => {
      expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    })
    // The order was read again and the page now shows the new status, announced.
    expect(state.gets).toBeGreaterThan(getsBefore)
    expect(screen.getByText('Confirmed', { selector: 'span' })).toBeVisible()
    expect(screen.getByText('Order status is now Confirmed.')).toBeVisible()
    expect(screen.queryByRole('button', { name: 'Cancel order' })).not.toBeInTheDocument()
  })

  it('shows no Cancel button for an order that is not Pending [US-F032 AC3]', async () => {
    signedInAs(customerUser)
    serveProducts({ [productId(1)]: makeProduct(1), [productId(2)]: makeProduct(2) })
    serveOrder(makeOrder({ ...order, status: 'Shipped' }))
    renderApp(url)

    await screen.findByRole('heading', { level: 1, name: 'Order #11111111' })

    expect(screen.queryByRole('button', { name: 'Cancel order' })).not.toBeInTheDocument()
    expect(screen.getByText('Only pending orders can be cancelled.')).toBeVisible()
  })

  it('shows the 429 countdown inside the dialog and keeps the button off', async () => {
    signedInAs(customerUser)
    serveProducts({ [productId(1)]: makeProduct(1), [productId(2)]: makeProduct(2) })
    serveOrder(order)
    server.use(
      http.post('*/api/v1/customers/me/orders/:orderId/cancel', () =>
        problem(429, 'rate-limited', {}, { 'Retry-After': '30' }),
      ),
    )
    renderApp(url)
    const { user, dialog } = await openCancelDialog()

    await user.click(within(dialog).getByRole('button', { name: 'Cancel order' }))

    expect(
      await within(dialog).findByText('Too many attempts. Try again in 30 seconds.'),
    ).toBeVisible()
    expect(within(dialog).getByRole('button', { name: 'Cancel order' })).toBeDisabled()
    expect(within(dialog).getByText('You can try again in 30 s')).toBeVisible()
  })

  it('does not close while the request runs and does not send twice', async () => {
    signedInAs(customerUser)
    serveProducts({ [productId(1)]: makeProduct(1), [productId(2)]: makeProduct(2) })
    const state = serveOrder(order)
    let release: () => void = () => undefined
    const gate = new Promise<void>((resolve) => {
      release = resolve
    })
    let posts = 0
    server.use(
      http.post('*/api/v1/customers/me/orders/:orderId/cancel', async () => {
        posts++
        await gate
        state.order = makeOrder({ ...order, status: 'Cancelled' })
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderApp(url)
    const { user, dialog } = await openCancelDialog()

    await user.dblClick(within(dialog).getByRole('button', { name: 'Cancel order' }))

    const busy = await within(dialog).findByRole('button', { name: 'Cancelling…' })
    expect(busy).toBeDisabled()
    release()
    await waitFor(() => {
      expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    })
    expect(posts).toBe(1)
  })
})
