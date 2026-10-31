import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { cartKeyFor, readCart, writeCart } from '@/lib/cart-storage'
import { customerUser, makeOrder, makeProduct, productId } from '@/test/data'
import { problem } from '@/test/problem'
import { renderApp } from '@/test/render-app'
import { server } from '@/test/server'
import { serveProducts, signedInAs } from '@/test/session'

if (!customerUser.customerId) throw new Error('Test customer needs a customer ID.')
const key = cartKeyFor(customerUser.customerId)
const line = (n: number, quantity = 2, price = 11.5) => ({
  productId: productId(n),
  quantity,
  price,
})
const stocked = (n: number, overrides = {}) =>
  makeProduct(n, { stockQuantity: 100, price: 11.5, ...overrides })

async function openCart(lines = [line(1)]) {
  signedInAs(customerUser)
  writeCart(key, lines)
  const view = renderApp('/cart')
  const placeOrder = await screen.findByRole('button', { name: 'Place order' })
  await waitFor(() => {
    expect(placeOrder).toBeEnabled()
  })
  return { ...view, placeOrder, user: userEvent.setup() }
}

describe('place order', () => {
  it('shows the created order with the total from the response and clears the cart [US-F029 AC1]', async () => {
    serveProducts({ [productId(1)]: stocked(1) })
    let body: unknown
    let csrf: string | null = null
    server.use(
      http.post('*/api/v1/customers/me/orders', async ({ request }) => {
        csrf = request.headers.get('X-CSRF')
        body = await request.json()
        // The server total wins over the estimate.
        return HttpResponse.json(makeOrder({ total: 22.5 }), { status: 201 })
      }),
      http.get('*/api/v1/customers/me/orders/:orderId', () =>
        HttpResponse.json(makeOrder({ total: 22.5 })),
      ),
    )
    const { placeOrder, user } = await openCart()

    await user.click(placeOrder)

    expect(await screen.findByRole('heading', { level: 1, name: 'Order #11111111' })).toBeVisible()
    expect(body).toEqual({ items: [{ productId: productId(1), quantity: 2 }] })
    expect(csrf).toBe('1')
    expect(screen.getByText('Pending', { selector: 'span' })).toBeVisible()
    expect(screen.getByText('Total $22.50 (USD)')).toBeVisible()
    expect(screen.getByText('Order placed. We will confirm it soon.')).toBeVisible()
    expect(readCart(key)).toEqual([])
    expect(screen.getByRole('link', { name: 'Cart, 0 products' })).toBeVisible()
  })

  it('shows the order and empties the cart at once, even when the background refresh stalls [Major 4]', async () => {
    serveProducts({ [productId(1)]: stocked(1) })
    server.use(
      http.post('*/api/v1/customers/me/orders', () =>
        HttpResponse.json(makeOrder(), { status: 201 }),
      ),
      http.get('*/api/v1/customers/me/orders/:orderId', () => HttpResponse.json(makeOrder())),
    )
    const { placeOrder, user } = await openCart()
    // From now on every product read hangs: the refresh after the order never finishes.
    server.use(
      http.get('*/api/v1/products/:productId', () => new Promise<Response>(() => undefined)),
      http.get('*/api/v1/products', () => new Promise<Response>(() => undefined)),
    )

    await user.click(placeOrder)

    expect(await screen.findByRole('heading', { level: 1, name: 'Order #11111111' })).toBeVisible()
    expect(readCart(key)).toEqual([])
  })

  it('shows the current price marked "Price updated" and the estimate from it [US-F029 AC2]', async () => {
    serveProducts({ [productId(1)]: stocked(1, { price: 12 }) })
    await openCart([line(1, 2, 11.5)])

    expect(await screen.findByText('Price updated (was $11.50)')).toBeVisible()
    expect(screen.getByText('Estimated total $24.00')).toBeVisible()
  })

  it('marks the products named by insufficient-stock and lets the customer fix them [US-F029 AC3]', async () => {
    serveProducts({ [productId(1)]: stocked(1), [productId(2)]: stocked(2) })
    server.use(
      http.post('*/api/v1/customers/me/orders', () =>
        problem(409, 'insufficient-stock', { productIds: [productId(2)] }),
      ),
    )
    const { placeOrder, user } = await openCart([line(1), line(2)])

    await user.click(placeOrder)

    expect(await screen.findByText(/Some products do not have enough stock/)).toBeVisible()
    const marked = await screen.findAllByText(/Not enough stock for this quantity/)
    expect(marked).toHaveLength(1)
    // Marked lines come first; the button works again.
    expect(screen.getByRole('button', { name: 'Place order' })).toBeEnabled()

    // Changing the quantity clears the mark.
    const quantity = screen.getByLabelText('Quantity of Sample product 2')
    await user.clear(quantity)
    await user.type(quantity, '1')
    await user.tab()
    await waitFor(() => {
      expect(screen.queryByText(/Not enough stock for this quantity/)).not.toBeInTheDocument()
    })
  })

  it('lets the customer remove a marked product', async () => {
    serveProducts({ [productId(1)]: stocked(1), [productId(2)]: stocked(2) })
    server.use(
      http.post('*/api/v1/customers/me/orders', () =>
        problem(409, 'insufficient-stock', { productIds: [productId(2)] }),
      ),
    )
    const { placeOrder, user } = await openCart([line(1), line(2)])
    await user.click(placeOrder)
    await screen.findAllByText(/Not enough stock for this quantity/)

    await user.click(screen.getByRole('button', { name: 'Remove Sample product 2' }))

    expect(screen.queryByText(/Not enough stock for this quantity/)).not.toBeInTheDocument()
    expect(readCart(key)).toHaveLength(1)
  })

  it('shows maxPending and a link to My orders for too-many-pending-orders [US-F029 AC4]', async () => {
    serveProducts({ [productId(1)]: stocked(1) })
    server.use(
      http.post('*/api/v1/customers/me/orders', () =>
        problem(409, 'too-many-pending-orders', { maxPending: 5 }),
      ),
    )
    const { placeOrder, user } = await openCart()

    await user.click(placeOrder)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(
      'You have 5 open orders. Wait for one to be confirmed or cancel one.',
    )
    expect(screen.getByRole('link', { name: 'View my orders' })).toHaveAttribute('href', '/orders')
    expect(readCart(key)).toHaveLength(1)
  })

  it('creates only one order when Place order is clicked twice [US-F029 AC5]', async () => {
    serveProducts({ [productId(1)]: stocked(1) })
    let release: () => void = () => undefined
    const gate = new Promise<void>((resolve) => {
      release = resolve
    })
    let posts = 0
    server.use(
      http.post('*/api/v1/customers/me/orders', async () => {
        posts++
        await gate
        return HttpResponse.json(makeOrder(), { status: 201 })
      }),
      http.get('*/api/v1/customers/me/orders/:orderId', () => HttpResponse.json(makeOrder())),
    )
    const { placeOrder, user } = await openCart()

    await user.dblClick(placeOrder)

    const busy = await screen.findByRole('button', { name: 'Placing order…' })
    expect(busy).toBeDisabled()
    expect(busy).toHaveAttribute('aria-busy', 'true')
    await user.click(busy)
    release()
    await screen.findByRole('heading', { level: 1, name: 'Order #11111111' })
    expect(posts).toBe(1)
  })

  it('does not retry after a network error and says to check My orders [NFR-F24]', async () => {
    serveProducts({ [productId(1)]: stocked(1) })
    let posts = 0
    server.use(
      http.post('*/api/v1/customers/me/orders', () => {
        posts++
        return HttpResponse.error()
      }),
    )
    const { placeOrder, user } = await openCart()

    await user.click(placeOrder)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Check My orders before trying again.')
    expect(within(alert).getByRole('link', { name: 'My orders' })).toHaveAttribute(
      'href',
      '/orders',
    )
    // Nothing is retried on its own, and the cart is kept.
    await new Promise((resolve) => setTimeout(resolve, 1200))
    expect(posts).toBe(1)
    expect(readCart(key)).toHaveLength(1)
    expect(screen.getByRole('button', { name: 'Place order' })).toBeEnabled()
  })

  it('marks a product the API says was removed (400 items[i].productId) [FR-F029]', async () => {
    serveProducts({ [productId(1)]: stocked(1), [productId(2)]: stocked(2) })
    server.use(
      http.post('*/api/v1/customers/me/orders', () =>
        problem(400, 'validation-failed', {
          errors: { 'items[1].productId': ['Product does not exist.'] },
        }),
      ),
    )
    const { placeOrder, user } = await openCart([line(1), line(2)])

    await user.click(placeOrder)

    expect(await screen.findByText('No longer available.')).toBeVisible()
    expect(screen.getByText('Remove unavailable products to continue.')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Place order' })).toBeDisabled()

    await user.click(screen.getByRole('button', { name: 'Remove' }))
    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Place order' })).toBeEnabled()
    })
    expect(readCart(key)).toHaveLength(1)
  })

  it('is off while a product is unavailable, with the reason [CU-3]', async () => {
    serveProducts({ [productId(1)]: stocked(1, { stockQuantity: 0 }) })
    signedInAs(customerUser)
    writeCart(key, [line(1)])
    renderApp('/cart')

    const button = await screen.findByRole('button', { name: 'Place order' })
    expect(await screen.findByText('Remove unavailable products to continue.')).toBeVisible()
    expect(button).toBeDisabled()
  })

  it('shows a countdown and keeps the button off after a 429 [NFR-F23]', async () => {
    serveProducts({ [productId(1)]: stocked(1) })
    server.use(
      http.post('*/api/v1/customers/me/orders', () =>
        problem(429, 'rate-limited', {}, { 'Retry-After': '30' }),
      ),
    )
    const { placeOrder, user } = await openCart()

    await user.click(placeOrder)

    expect(await screen.findByText('Too many attempts. Try again in 30 seconds.')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Place order' })).toBeDisabled()
  })

  it('shows a generic message and the reference for a server error', async () => {
    serveProducts({ [productId(1)]: stocked(1) })
    server.use(http.post('*/api/v1/customers/me/orders', () => problem(500, 'internal-error')))
    const { placeOrder, user } = await openCart()

    await user.click(placeOrder)

    expect(await screen.findByText('Something went wrong. Try again.')).toBeVisible()
    expect(screen.getByText('Reference: trace-test-123')).toBeVisible()
  })
})
