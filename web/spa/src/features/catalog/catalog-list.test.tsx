import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { customerUser, makeProduct } from '@/test/data'
import { problem } from '@/test/problem'
import { renderApp } from '@/test/render-app'
import { server } from '@/test/server'
import { page, signedInAs } from '@/test/session'

// Records every EP-22 request so tests can check what was (not) sent.
function recordList(respond: (query: URLSearchParams) => Response = () => page([makeProduct(1)])) {
  const queries: URLSearchParams[] = []
  server.use(
    http.get('*/api/v1/products', ({ request }) => {
      const query = new URL(request.url).searchParams
      queries.push(query)
      return respond(query)
    }),
  )
  return queries
}

describe('catalog search, filters and paging', () => {
  it('sends a search of 2 or more characters and shows the total count [US-F026 AC1]', async () => {
    signedInAs(customerUser)
    const queries = recordList((query) =>
      query.get('search') === 'wid'
        ? page([makeProduct(3, { name: 'Widget' })], 1)
        : page([makeProduct(1), makeProduct(2)], 134),
    )
    const { router } = renderApp('/products')
    const user = userEvent.setup()
    expect(await screen.findByText('134 products')).toBeVisible()

    await user.type(screen.getByLabelText('Search name or SKU'), 'wid')

    expect(await screen.findByText('Widget')).toBeVisible()
    expect(screen.getByText('1 product')).toBeVisible()
    expect(queries.at(-1)?.get('search')).toBe('wid')
    // The search is in the URL, and typing did not send a request per keystroke.
    expect(router.state.location.search).toBe('?search=wid')
    // Exactly one request for the search, and none for the partial text.
    expect(queries.filter((query) => query.has('search')).map((q) => q.get('search'))).toEqual([
      'wid',
    ])
  })

  it('sends nothing for 1 character and shows a hint [US-F026 AC2]', async () => {
    signedInAs(customerUser)
    const queries = recordList()
    const { router } = renderApp('/products')
    const user = userEvent.setup()
    await screen.findByText('Sample product 1')

    await user.type(screen.getByLabelText('Search name or SKU'), 'a')

    expect(screen.getByText('Type at least 2 characters')).toBeVisible()
    expect(screen.getByLabelText('Search name or SKU')).toHaveAccessibleDescription(
      'Type at least 2 characters',
    )
    // Wait longer than the debounce: still no search.
    await new Promise((resolve) => setTimeout(resolve, 450))
    expect(queries.some((query) => query.has('search'))).toBe(false)
    expect(router.state.location.search).toBe('')
  })

  it('shows a field error and makes no call when min price is above max price [US-F026 AC3]', async () => {
    signedInAs(customerUser)
    const queries = recordList()
    renderApp('/products')
    const user = userEvent.setup()
    await screen.findByText('Sample product 1')
    const callsBefore = queries.length

    await user.type(screen.getByLabelText('Min price'), '50')
    await user.type(screen.getByLabelText('Max price'), '10')
    await user.click(screen.getByRole('button', { name: 'Apply filters' }))

    const min = screen.getByLabelText('Min price')
    await waitFor(() => {
      expect(min).toHaveAccessibleDescription('Min price must be less than or equal to max price.')
    })
    expect(min).toHaveFocus()
    expect(queries).toHaveLength(callsBefore)
  })

  it('applies the price range and in-stock filter, in the URL and in the request [US-F026]', async () => {
    signedInAs(customerUser)
    const queries = recordList()
    const { router } = renderApp('/products?page=3')
    const user = userEvent.setup()
    await screen.findByText('Sample product 1')

    await user.type(screen.getByLabelText('Min price'), '5')
    await user.type(screen.getByLabelText('Max price'), '20.50')
    await user.click(screen.getByLabelText('In stock only'))
    await user.click(screen.getByRole('button', { name: 'Apply filters' }))

    await waitFor(() => {
      expect(router.state.location.search).toBe('?minPrice=5&maxPrice=20.5&inStock=true')
    })
    await waitFor(() => {
      const last = queries.at(-1)
      expect(last?.get('minPrice')).toBe('5')
      expect(last?.get('maxPrice')).toBe('20.5')
      expect(last?.get('inStock')).toBe('true')
      // A new filter goes back to page 1.
      expect(last?.get('page')).toBe('1')
    })
    expect(screen.getByText('3 filters applied')).toBeVisible()
  })

  it('sorts by price and goes back to page 1 [US-F026]', async () => {
    signedInAs(customerUser)
    const queries = recordList()
    const { router } = renderApp('/products?page=2')
    const user = userEvent.setup()
    await screen.findByText('Sample product 1')

    await user.selectOptions(screen.getByLabelText('Sort'), 'Price high to low')

    await waitFor(() => {
      expect(queries.at(-1)?.get('sort')).toBe('-price')
    })
    expect(router.state.location.search).toBe('?sort=-price')
  })

  it('reads filters, sort and page from the URL and sends them to the API [US-F026]', async () => {
    signedInAs(customerUser)
    const queries = recordList(() => page([makeProduct(1)], 45, 2))
    renderApp('/products?search=ab&minPrice=5&maxPrice=9.99&inStock=true&sort=-price&page=2')

    await screen.findByText('Page 2 of 3')

    const query = queries[0]
    expect(query?.get('search')).toBe('ab')
    expect(query?.get('minPrice')).toBe('5')
    expect(query?.get('maxPrice')).toBe('9.99')
    expect(query?.get('inStock')).toBe('true')
    expect(query?.get('sort')).toBe('-price')
    expect(query?.get('page')).toBe('2')
    expect(query?.get('pageSize')).toBe('20')
    expect(screen.getByLabelText('Search name or SKU')).toHaveValue('ab')
    expect(screen.getByLabelText('Min price')).toHaveValue('5')
    expect(screen.getByLabelText('In stock only')).toBeChecked()
    expect(screen.getByLabelText('Sort')).toHaveValue('-price')
  })

  it('ignores bad values in the URL', async () => {
    signedInAs(customerUser)
    const queries = recordList()
    renderApp('/products?sort=bogus&page=-3&minPrice=abc&search=x&inStock=maybe')

    await screen.findByText('Sample product 1')

    const query = queries[0]
    expect(query?.get('sort')).toBe('name')
    expect(query?.get('page')).toBe('1')
    expect(query?.has('minPrice')).toBe(false)
    expect(query?.has('search')).toBe(false)
    expect(query?.has('inStock')).toBe(false)
  })

  it('keeps the filters when paging', async () => {
    signedInAs(customerUser)
    recordList(() => page([makeProduct(1)], 45))
    renderApp('/products?inStock=true&sort=price')

    const next = await screen.findByRole('link', { name: 'Next' })

    expect(next).toHaveAttribute('href', '/products?inStock=true&sort=price&page=2')
  })

  it('shows "No products" and a link to page 1 for a page past the end [US-F026 AC4]', async () => {
    signedInAs(customerUser)
    recordList(() => page([], 3, 9))
    renderApp('/products?page=9')

    expect(await screen.findByText('No products on this page.')).toBeVisible()
    expect(screen.getByRole('link', { name: 'Go to page 1' })).toHaveAttribute('href', '/products')
  })

  it('offers "Clear filters" when nothing matches', async () => {
    signedInAs(customerUser)
    recordList((query) => (query.has('search') ? page([], 0) : page([makeProduct(1)])))
    const { router } = renderApp('/products?search=zzz')
    const user = userEvent.setup()

    expect(await screen.findByText('No products match your search.')).toBeVisible()
    // The toolbar and the empty state each have the button.
    await user.click(screen.getAllByRole('button', { name: 'Clear filters' })[0] as HTMLElement)

    expect(await screen.findByText('Sample product 1')).toBeVisible()
    expect(router.state.location.search).toBe('')
    expect(screen.getByLabelText('Search name or SKU')).toHaveValue('')
  })

  it('shows a loading state, then the list', async () => {
    signedInAs(customerUser)
    let release: () => void = () => undefined
    const gate = new Promise<void>((resolve) => {
      release = resolve
    })
    server.use(
      http.get('*/api/v1/products', async () => {
        await gate
        return page([makeProduct(1)])
      }),
    )
    renderApp('/products')

    expect(await screen.findByText('Loading products…')).toBeInTheDocument()
    release()
    expect(await screen.findByText('Sample product 1')).toBeVisible()
  })

  it('shows a network failure with a retry [NFR-F21]', async () => {
    signedInAs(customerUser)
    server.use(http.get('*/api/v1/products', () => HttpResponse.error()))
    renderApp('/products')

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Could not reach the server. Check your connection')
    server.use(http.get('*/api/v1/products', () => page([makeProduct(1)])))
    await userEvent.setup().click(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByText('Sample product 1')).toBeVisible()
  })

  it('shows a 400 from the API as an error with a retry', async () => {
    signedInAs(customerUser)
    server.use(http.get('*/api/v1/products', () => problem(400, 'validation-failed')))
    renderApp('/products')

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Check the highlighted fields and try again.',
    )
  })
})
