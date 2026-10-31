import { http, HttpResponse } from 'msw'
import type { SessionUser } from '@/lib/session'
import { makeProduct } from '@/test/data'
import { problem } from '@/test/problem'
import { server } from '@/test/server'

// The BFF says this user is signed in.
export function signedInAs(user: SessionUser) {
  server.use(http.get('*/bff/user', () => HttpResponse.json(user)))
}

export function page(items: unknown[], totalCount = items.length, pageNumber = 1, pageSize = 20) {
  return HttpResponse.json({
    items,
    page: pageNumber,
    pageSize,
    totalCount,
    totalPages: Math.ceil(totalCount / pageSize),
  })
}

// Serves products by ID (EP-23) and as a list (EP-22). Unknown IDs are 404.
export function serveProducts(products: Record<string, ReturnType<typeof makeProduct>>) {
  server.use(
    http.get('*/api/v1/products/:productId', ({ params }) => {
      const found = products[String(params.productId)]
      return found ? HttpResponse.json(found) : problem(404, 'not-found')
    }),
    http.get('*/api/v1/products', () => page(Object.values(products))),
  )
}
