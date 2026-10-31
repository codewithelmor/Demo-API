import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { customerUser, makeProduct } from '@/test/data'
import { problem } from '@/test/problem'
import { renderApp } from '@/test/render-app'
import { server } from '@/test/server'
import { page, signedInAs } from '@/test/session'

const product = makeProduct(1, { description: 'A sample description.', stockQuantity: 7 })
const url = `/products/${product.id}`

function productIs(value: object) {
  server.use(http.get('*/api/v1/products/:productId', () => HttpResponse.json(value)))
}

describe('product detail', () => {
  it('shows name, SKU, description, price and stock [US-F027 AC1]', async () => {
    signedInAs(customerUser)
    productIs(product)
    renderApp(url)

    expect(await screen.findByRole('heading', { level: 1, name: 'Sample product 1' })).toBeVisible()
    expect(screen.getByText('SKU SKU-1001')).toBeVisible()
    expect(screen.getByText('A sample description.')).toBeVisible()
    expect(screen.getByText('$11.50')).toBeVisible()
    expect(screen.getByText('In stock')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Add to cart' })).toBeEnabled()
  })

  it('shows "Out of stock" and disables adding [US-F027 AC1]', async () => {
    signedInAs(customerUser)
    productIs({ ...product, stockQuantity: 0, description: null })
    renderApp(url)

    expect(await screen.findByText('Out of stock', { selector: 'span' })).toBeVisible()
    expect(screen.getByRole('button', { name: 'Out of stock' })).toBeDisabled()
    expect(screen.getByText('No description')).toBeVisible()
    expect(screen.queryByLabelText('Quantity')).not.toBeInTheDocument()
  })

  it('shows "Product not found" for a deleted product (404) [US-F027 AC2]', async () => {
    signedInAs(customerUser)
    server.use(http.get('*/api/v1/products/:productId', () => problem(404, 'not-found')))
    renderApp(url)

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Product not found' }),
    ).toBeVisible()
    expect(screen.getByText('Product not found. It may have been removed.')).toBeVisible()
    expect(screen.getByRole('link', { name: 'Back to products' })).toHaveAttribute(
      'href',
      '/products',
    )
  })

  it('shows a retry for a server error', async () => {
    signedInAs(customerUser)
    server.use(http.get('*/api/v1/products/:productId', () => problem(500, 'internal-error')))
    renderApp(url)

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong. Try again.')
    expect(screen.getByRole('button', { name: 'Try again' })).toBeVisible()
  })

  it('goes back to the same list query [CU-2]', async () => {
    signedInAs(customerUser)
    productIs(product)
    server.use(http.get('*/api/v1/products', () => page([product])))
    const { router } = renderApp('/products?sort=-price&inStock=true')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('link', { name: 'Sample product 1' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Sample product 1' })).toBeVisible()
    await user.click(screen.getByRole('link', { name: 'Back to products' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Products' })).toBeVisible()
    expect(router.state.location.search).toBe('?sort=-price&inStock=true')
  })

  it('adds the chosen quantity and announces it [US-F028 AC1]', async () => {
    signedInAs(customerUser)
    productIs(product)
    renderApp(url)
    const user = userEvent.setup()
    await screen.findByRole('heading', { level: 1, name: 'Sample product 1' })

    await user.click(screen.getByRole('button', { name: 'Increase quantity' }))
    await user.click(screen.getByRole('button', { name: 'Increase quantity' }))
    await user.click(screen.getByRole('button', { name: 'Add to cart' }))

    expect(
      await screen.findByText('Added Sample product 1 to cart. 1 product in cart.'),
    ).toBeVisible()
    expect(screen.getByText('3 in cart')).toBeVisible()
    expect(screen.getByRole('link', { name: 'Cart, 1 product' })).toBeVisible()
  })

  it('limits the quantity to the stock [US-F028 AC3]', async () => {
    signedInAs(customerUser)
    productIs(product)
    renderApp(url)
    const user = userEvent.setup()
    const quantity = await screen.findByLabelText('Quantity')

    await user.clear(quantity)
    await user.type(quantity, '8')

    expect(quantity).toHaveAccessibleDescription('Enter a quantity from 1 to 7.')
    expect(quantity).toHaveAttribute('aria-invalid', 'true')
  })
})
