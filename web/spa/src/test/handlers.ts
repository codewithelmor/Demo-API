import { http, HttpResponse } from 'msw'
import { makeProduct } from '@/test/data'

// Default handlers: nobody is signed in, and the catalog has two products.
// Tests override per case with server.use(...). Unhandled requests fail the test (setup.ts).
export const handlers = [
  http.get('*/bff/user', () =>
    HttpResponse.json(
      { type: '/problems/not-signed-in', title: 'Not signed in', status: 401 },
      { status: 401, headers: { 'Content-Type': 'application/problem+json' } },
    ),
  ),

  http.get('*/api/v1/products', () =>
    HttpResponse.json({
      items: [makeProduct(1), makeProduct(2)],
      page: 1,
      pageSize: 12,
      totalCount: 2,
      totalPages: 1,
    }),
  ),
]
