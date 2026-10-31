import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { adminUser, customerUser, makeProduct, productId } from '@/test/data'
import { problem } from '@/test/problem'
import { renderApp } from '@/test/render-app'
import { server } from '@/test/server'
import { page, signedInAs } from '@/test/session'

describe('back office dashboard', () => {
  it('shows the low-stock count and the last 30 days of sales [FR-F050]', async () => {
    signedInAs(adminUser)
    renderApp('/admin')

    expect(await screen.findByRole('heading', { level: 1, name: 'Back office' })).toBeVisible()
    const stock = await screen.findByText('Products low on stock')
    expect(stock.nextElementSibling).toHaveTextContent('3')
    expect((await screen.findByText('Revenue')).nextElementSibling).toHaveTextContent('$120.50')
    expect(screen.getByText('Orders').nextElementSibling).toHaveTextContent('4')
  })

  it('shows one failing report with a retry, and still shows the other', async () => {
    signedInAs(adminUser)
    server.use(http.get('*/api/v1/reports/low-stock', () => problem(500, 'internal-error')))
    renderApp('/admin')

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong. Try again.')
    expect(await screen.findByText('Revenue')).toBeVisible()
  })

  it('is not shown to a Customer', async () => {
    signedInAs(customerUser)
    renderApp('/admin/products')

    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: 'You do not have access to this page.',
      }),
    ).toBeVisible()
  })
})

describe('admin product list', () => {
  const active = makeProduct(1)
  const deleted = makeProduct(2, { isDeleted: true })

  it('asks the API for deleted products only when "Include deleted" is on, and marks them', async () => {
    signedInAs(adminUser)
    const queries: (string | null)[] = []
    server.use(
      http.get('*/api/v1/products', ({ request }) => {
        const url = new URL(request.url)
        queries.push(url.searchParams.get('includeDeleted'))
        return url.searchParams.get('includeDeleted') === 'true'
          ? page([active, deleted])
          : page([active])
      }),
    )
    renderApp('/admin/products')
    const user = userEvent.setup()

    expect(await screen.findByRole('link', { name: 'Edit Sample product 1' })).toBeVisible()
    expect(screen.queryByText('Deleted')).not.toBeInTheDocument()

    await user.click(screen.getByRole('checkbox', { name: 'Include deleted' }))

    const row = (await screen.findByRole('rowheader', { name: 'Sample product 2' })).closest('tr')
    if (!row) throw new Error('Row not found.')
    expect(within(row).getByText('Deleted')).toBeVisible()
    expect(within(row).getByRole('button', { name: 'Restore Sample product 2' })).toBeVisible()
    expect(within(row).queryByRole('link', { name: /^Edit/ })).not.toBeInTheDocument()
    expect(queries).toContain('true')
  })

  it('deletes a product after confirmation and reloads the list', async () => {
    signedInAs(adminUser)
    let gone = false
    const deletes: string[] = []
    server.use(
      http.get('*/api/v1/products', () => page(gone ? [] : [active])),
      http.delete('*/api/v1/products/:id', ({ params, request }) => {
        deletes.push(`${String(params.id)} csrf=${request.headers.get('X-CSRF') ?? ''}`)
        gone = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderApp('/admin/products')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Delete Sample product 1' }))
    await user.click(await screen.findByRole('button', { name: 'Delete product' }))

    expect(await screen.findByText('No products found.')).toBeVisible()
    expect(deletes).toEqual([`${productId(1)} csrf=1`])
  })

  it('shows the reason when a restore hits a duplicate SKU (409)', async () => {
    signedInAs(adminUser)
    server.use(
      http.get('*/api/v1/products', () => page([deleted])),
      http.post('*/api/v1/products/:id/restore', () => problem(409, 'duplicate-sku')),
    )
    renderApp('/admin/products?includeDeleted=true')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Restore Sample product 2' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Another active product already uses this SKU.',
    )
  })
})

describe('create product', () => {
  async function fill(user: ReturnType<typeof userEvent.setup>) {
    await user.type(await screen.findByLabelText('Name'), 'Desk lamp')
    await user.type(screen.getByLabelText(/^SKU/), 'lamp-1')
    await user.type(screen.getByLabelText('Price'), '24.9')
    await user.clear(screen.getByLabelText('Stock quantity'))
    await user.type(screen.getByLabelText('Stock quantity'), '7')
  }

  it('validates, then posts the product and returns to the list', async () => {
    signedInAs(adminUser)
    const bodies: unknown[] = []
    server.use(
      http.post('*/api/v1/products', async ({ request }) => {
        bodies.push(await request.json())
        return HttpResponse.json(makeProduct(3), { status: 201, headers: { ETag: '"1"' } })
      }),
    )
    renderApp('/admin/products/new')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Create product' }))
    expect(await screen.findByText('Enter a name.')).toBeVisible()
    expect(bodies).toHaveLength(0)

    await fill(user)
    await user.click(screen.getByRole('button', { name: 'Create product' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Products' })).toBeVisible()
    expect(bodies).toEqual([
      { name: 'Desk lamp', sku: 'lamp-1', description: null, price: 24.9, stockQuantity: 7 },
    ])
  })

  it('shows a duplicate SKU (409) on the SKU field', async () => {
    signedInAs(adminUser)
    server.use(
      http.post('*/api/v1/products', () =>
        problem(409, 'duplicate-sku', { errors: { sku: ['SKU is already used.'] } }),
      ),
    )
    renderApp('/admin/products/new')
    const user = userEvent.setup()

    await fill(user)
    await user.click(screen.getByRole('button', { name: 'Create product' }))

    expect(await screen.findByText('SKU is already used.')).toBeVisible()
    expect(screen.getByLabelText(/^SKU/)).toBeInvalid()
  })
})

describe('edit product', () => {
  const product = makeProduct(1, { stockQuantity: 5 })

  it('sends If-Match from the ETag when saving [FR-F053]', async () => {
    signedInAs(adminUser)
    const puts: { ifMatch: string | null; body: unknown }[] = []
    server.use(
      http.get('*/api/v1/products/:id', () =>
        HttpResponse.json(product, { headers: { ETag: '"7"' } }),
      ),
      http.put('*/api/v1/products/:id', async ({ request }) => {
        puts.push({ ifMatch: request.headers.get('If-Match'), body: await request.json() })
        return HttpResponse.json(product, { headers: { ETag: '"8"' } })
      }),
    )
    renderApp(`/admin/products/${productId(1)}`)
    const user = userEvent.setup()

    const stock = await screen.findByLabelText('Stock quantity')
    expect(stock).toHaveValue('5')
    await user.clear(stock)
    await user.type(stock, '12')
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() => {
      expect(puts).toHaveLength(1)
    })
    expect(puts[0]?.ifMatch).toBe('"7"')
    expect(puts[0]?.body).toMatchObject({ stockQuantity: 12, price: 11.5 })
  })

  it('reloads the latest values after a conflict (412) and saves with the new ETag', async () => {
    signedInAs(adminUser)
    let etag = '"7"'
    let latest = product
    const ifMatches: (string | null)[] = []
    server.use(
      http.get('*/api/v1/products/:id', () =>
        HttpResponse.json(latest, { headers: { ETag: etag } }),
      ),
      http.put('*/api/v1/products/:id', ({ request }) => {
        ifMatches.push(request.headers.get('If-Match'))
        return ifMatches.length === 1
          ? problem(412, 'precondition-failed')
          : HttpResponse.json(latest, { headers: { ETag: '"9"' } })
      }),
    )
    renderApp(`/admin/products/${productId(1)}`)
    const user = userEvent.setup()

    await screen.findByLabelText('Name')
    // Someone else renames the product and bumps the ETag.
    latest = { ...product, name: 'Renamed elsewhere' }
    etag = '"8"'
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText('This product changed since you opened it.')).toBeVisible()
    expect(screen.getByLabelText('Name')).toHaveValue('Renamed elsewhere')

    await user.click(screen.getByRole('button', { name: 'Save changes' }))
    await waitFor(() => {
      expect(ifMatches).toEqual(['"7"', '"8"'])
    })
  })

  it('shows "we could not find that" for an unknown product (404)', async () => {
    signedInAs(adminUser)
    server.use(http.get('*/api/v1/products/:id', () => problem(404, 'not-found')))
    renderApp(`/admin/products/${productId(9)}`)

    expect(await screen.findByRole('alert')).toHaveTextContent('We could not find that.')
  })
})
