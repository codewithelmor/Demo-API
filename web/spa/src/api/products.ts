/** Product endpoints of the API, plus the cache keys for product data. */
import { queryOptions } from '@tanstack/react-query'
import { api, unwrap } from '@/api/client'
import type { components } from '@/api/schema'

export type Product = components['schemas']['ProductResponse']

/**
 * Cache keys for product data (see `orderKeys` for what a key is). Shared by the catalog (list, detail)
 * and the cart (re-reads every line), which cannot import each other.
 */
export const productKeys = {
  all: ['products'] as const,
  list: (params: object) => [...productKeys.all, 'list', params] as const,
  detail: (id: string) => [...productKeys.all, 'detail', id] as const,
}

/** Query definition for one product: GET /api/v1/products/{productId} (EP-23). The BFF adds the bearer token. */
export function productQuery(productId: string) {
  return queryOptions({
    queryKey: productKeys.detail(productId),
    queryFn: ({ signal }): Promise<Product> =>
      unwrap<Product>(
        api.GET('/api/v1/products/{productId}', { params: { path: { productId } }, signal }),
      ),
  })
}
