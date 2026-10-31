import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { adminUser, customerUser, makeProduct } from '@/test/data'
import { problem } from '@/test/problem'
import { renderApp } from '@/test/render-app'
import { server } from '@/test/server'
import { page, signedInAs } from '@/test/session'
import { cartKeyFor, readCart, writeCart } from '@/lib/cart-storage'

if (!customerUser.customerId) throw new Error('Test customer needs a customer ID.')
const key = cartKeyFor(customerUser.customerId)
const idOf = (n: number) => `00000000-0000-4000-8000-${String(n).padStart(12, '0')}`

// Products are served by ID from this table (the cart page reads each one).
function catalog(products: Record<string, ReturnType<typeof makeProduct>>) {
  server.use(
    http.get('*/api/v1/products/:productId', ({ params }) => {
      const found = products[String(params.productId)]
      return found ? HttpResponse.json(found) : problem(404, 'not-found')
    }),
    http.get('*/api/v1/products', () => page(Object.values(products))),
  )
}

describe('adding to the cart', () => {
  it('announces the same message again when the same change repeats [Minor 3]', async () => {
    signedInAs(customerUser)
    catalog({ [idOf(1)]: makeProduct(1, { stockQuantity: 1 }) })
    renderApp('/products')
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Add to cart: Sample product 1' }))
    const first = await screen.findByText('Added Sample product 1 to cart. 1 product in cart.')

    // Remove from the cart page and add again: the same text must be a new node in the live region.
    await user.click(screen.getByRole('link', { name: 'Cart, 1 product' }))
    await user.click(await screen.findByRole('button', { name: /^Remove/ }))
    const undo = await screen.findByRole('button', { name: 'Undo' })
    expect(undo).toHaveFocus()
    await user.click(screen.getByRole('link', { name: 'Browse products' }))
    await user.click(await screen.findByRole('button', { name: /^Add to cart: Sample product 1/ }))
    const second = await screen.findByText('Added Sample product 1 to cart. 1 product in cart.')

    expect(second).not.toBe(first)
  })

  it('updates the count, announces it and stores only id, quantity and price [US-F028 AC1]', async () => {
    signedInAs(customerUser)
    catalog({ [idOf(1)]: makeProduct(1) })
    renderApp('/products')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Add to cart: Sample product 1' }))

    expect(
      await screen.findByText('Added Sample product 1 to cart. 1 product in cart.'),
    ).toBeVisible()
    expect(screen.getByRole('link', { name: 'Cart, 1 product' })).toBeVisible()
    expect(screen.getByText('1 in cart')).toBeVisible()
    expect(JSON.parse(window.localStorage.getItem(key) ?? '')).toEqual({
      lines: [{ productId: idOf(1), quantity: 1, price: 11.5 }],
    })

    await user.click(screen.getByRole('button', { name: 'Add one more: Sample product 1' }))
    expect(await screen.findByText('2 in cart')).toBeVisible()
    expect(readCart(key)[0]?.quantity).toBe(2)
    // Still one distinct product.
    expect(screen.getByRole('link', { name: 'Cart, 1 product' })).toBeVisible()
  })

  it('does not add past the stock', async () => {
    signedInAs(customerUser)
    catalog({ [idOf(1)]: makeProduct(1, { stockQuantity: 1 }) })
    renderApp('/products')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Add to cart: Sample product 1' }))
    await user.click(await screen.findByRole('button', { name: 'Add one more: Sample product 1' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('That is all we have in stock.')
    expect(readCart(key)[0]?.quantity).toBe(1)
  })

  it('refuses a 51st distinct product with "Max 50 products per order" [US-F028 AC2]', async () => {
    signedInAs(customerUser)
    writeCart(
      key,
      Array.from({ length: 50 }, (_, i) => ({ productId: idOf(i + 100), quantity: 1, price: 5 })),
    )
    catalog({ [idOf(1)]: makeProduct(1) })
    renderApp('/products')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Add to cart: Sample product 1' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Max 50 products per order')
    expect(readCart(key)).toHaveLength(50)
    expect(screen.getByRole('link', { name: 'Cart, 50 products' })).toBeVisible()
  })

  it('has no cart for an Admin', async () => {
    signedInAs(adminUser)
    renderApp('/admin')

    await screen.findByRole('heading', { level: 1, name: 'Back office' })

    expect(screen.queryByRole('link', { name: /^Cart/ })).not.toBeInTheDocument()
  })
})

describe('the cart page', () => {
  const stocked = makeProduct(1, { stockQuantity: 5000 })

  it('shows an empty state with a link to the catalog', async () => {
    signedInAs(customerUser)
    renderApp('/cart')

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Cart (0 products)' }),
    ).toBeVisible()
    expect(screen.getByText('Your cart is empty. Browse products to add some.')).toBeVisible()
    expect(screen.getByRole('link', { name: 'Browse products' })).toHaveAttribute(
      'href',
      '/products',
    )
  })

  it('shows names, prices, line totals and the estimated total', async () => {
    signedInAs(customerUser)
    catalog({ [idOf(1)]: stocked, [idOf(2)]: makeProduct(2, { stockQuantity: 9 }) })
    writeCart(key, [
      { productId: idOf(1), quantity: 2, price: 11.5 },
      { productId: idOf(2), quantity: 1, price: 12.5 },
    ])
    renderApp('/cart')

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Cart (2 products)' }),
    ).toBeVisible()
    expect(await screen.findByRole('link', { name: 'Sample product 1' })).toBeVisible()
    expect(screen.getByText('$23.00')).toBeVisible() // 2 x 11.50
    expect(screen.getByText('Estimated total $35.50')).toBeVisible()
    expect(screen.getByText('Final total is confirmed when you place the order.')).toBeVisible()
  })

  it('changes the quantity and saves it [US-F028]', async () => {
    signedInAs(customerUser)
    catalog({ [idOf(1)]: stocked })
    writeCart(key, [{ productId: idOf(1), quantity: 2, price: 11.5 }])
    renderApp('/cart')
    const user = userEvent.setup()

    const quantity = await screen.findByLabelText('Quantity of Sample product 1')
    await user.clear(quantity)
    await user.type(quantity, '5')
    await user.tab() // a typed quantity is saved when the field loses focus

    expect(await screen.findByText('Estimated total $57.50')).toBeVisible()
    expect(readCart(key)[0]?.quantity).toBe(5)

    await user.click(screen.getByRole('button', { name: 'Increase quantity of sample product 1' }))
    expect(readCart(key)[0]?.quantity).toBe(6)
  })

  it('shows an error and keeps the cart for a quantity above 1000 or below 1 [US-F028 AC3]', async () => {
    signedInAs(customerUser)
    catalog({ [idOf(1)]: stocked })
    writeCart(key, [{ productId: idOf(1), quantity: 2, price: 11.5 }])
    renderApp('/cart')
    const user = userEvent.setup()
    const quantity = await screen.findByLabelText('Quantity of Sample product 1')

    await user.clear(quantity)
    await user.type(quantity, '1001')
    expect(quantity).toHaveAccessibleDescription('Enter a quantity from 1 to 1000.')
    expect(quantity).toHaveAttribute('aria-invalid', 'true')

    await user.clear(quantity)
    await user.type(quantity, '0')
    expect(quantity).toHaveAccessibleDescription('Enter a quantity from 1 to 1000.')

    await user.clear(quantity)
    await user.type(quantity, 'abc')
    expect(quantity).toHaveAccessibleDescription('Enter a quantity from 1 to 1000.')

    expect(readCart(key)[0]?.quantity).toBe(2)
  })

  it('removes a line and can undo it', async () => {
    signedInAs(customerUser)
    catalog({ [idOf(1)]: stocked })
    writeCart(key, [{ productId: idOf(1), quantity: 2, price: 11.5 }])
    renderApp('/cart')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Remove Sample product 1' }))

    expect(
      await screen.findByText('Your cart is empty. Browse products to add some.'),
    ).toBeVisible()
    expect(screen.getByText('Removed Sample product 1.')).toBeVisible()
    expect(readCart(key)).toEqual([])

    await user.click(screen.getByRole('button', { name: 'Undo' }))
    expect(await screen.findByRole('link', { name: 'Sample product 1' })).toBeVisible()
    expect(readCart(key)).toEqual([{ productId: idOf(1), quantity: 2, price: 11.5 }])
  })

  it('marks a changed price, a missing product and low stock, problems first', async () => {
    signedInAs(customerUser)
    catalog({
      [idOf(1)]: makeProduct(1, { price: 12, stockQuantity: 100 }), // was 10
      [idOf(3)]: makeProduct(3, { stockQuantity: 1 }), // wants 4
    })
    writeCart(key, [
      { productId: idOf(1), quantity: 1, price: 10 },
      { productId: idOf(2), quantity: 1, price: 10 }, // deleted: 404
      { productId: idOf(3), quantity: 4, price: 13.5 },
    ])
    renderApp('/cart')

    expect(await screen.findByText('Price updated (was $10.00)')).toBeVisible()
    expect(await screen.findByText('No longer available.')).toBeVisible()
    expect(screen.getByText('Only 1 left.')).toBeVisible()
    // Lines with problems come first; the line without one is last.
    const items = within(screen.getByRole('main')).getAllByRole('listitem')
    expect(items.at(-1)).toHaveTextContent('Sample product 1')

    await userEvent.setup().click(screen.getByRole('button', { name: 'Set to 1' }))
    expect(readCart(key).find((l) => l.productId === idOf(3))?.quantity).toBe(1)
  })

  it('is not for an Admin', async () => {
    signedInAs(adminUser)
    renderApp('/cart')

    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: 'You do not have access to this page.',
      }),
    ).toBeVisible()
  })
})

describe('the cart and the session', () => {
  it('is empty after signing out and in again [US-F028 AC4]', async () => {
    signedInAs(customerUser)
    catalog({ [idOf(1)]: makeProduct(1) })
    server.use(
      http.post('*/bff/logout', () => new HttpResponse(null, { status: 204 })),
      http.post('*/bff/login', () => HttpResponse.json(customerUser)),
    )
    renderApp('/products')
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Add to cart: Sample product 1' }))
    expect(await screen.findByRole('link', { name: 'Cart, 1 product' })).toBeVisible()

    await user.click(screen.getByRole('button', { name: 'Sign out' }))
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    expect(window.localStorage.getItem(key)).toBeNull()

    await user.type(screen.getByLabelText('Email'), customerUser.email)
    await user.type(screen.getByLabelText('Password'), 'correct horse battery staple')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('link', { name: 'Cart, 0 products' })).toBeVisible()
    expect(readCart(key)).toEqual([])
  })

  it('is still there after the session ended and the user signs in again [US-F028 AC5]', async () => {
    signedInAs(customerUser)
    writeCart(key, [{ productId: idOf(1), quantity: 3, price: 11.5 }])
    let calls = 0
    server.use(
      http.get('*/api/v1/products', () => {
        calls++
        return calls === 1 ? problem(401, 'session-ended') : page([makeProduct(1)])
      }),
      http.post('*/bff/login', () => HttpResponse.json(customerUser)),
    )
    renderApp('/products')

    // The BFF session ended: the app goes to sign-in, and the cart is not touched.
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    expect(readCart(key)).toHaveLength(1)

    const user = userEvent.setup()
    await user.type(screen.getByLabelText('Email'), customerUser.email)
    await user.type(screen.getByLabelText('Password'), 'correct horse battery staple')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('link', { name: 'Cart, 1 product' })).toBeVisible()
    await waitFor(() => {
      expect(screen.getByText('3 in cart')).toBeVisible()
    })
  })

  it('ignores an invalid stored cart instead of failing [NFR-F41]', async () => {
    signedInAs(customerUser)
    catalog({ [idOf(1)]: makeProduct(1) })
    window.localStorage.setItem(
      key,
      JSON.stringify({ lines: [{ productId: 'x', quantity: 99999 }] }),
    )
    renderApp('/products')

    expect(await screen.findByRole('link', { name: 'Cart, 0 products' })).toBeVisible()
    expect(await screen.findByText('Sample product 1')).toBeVisible()
  })
})
