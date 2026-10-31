import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api, unwrap } from '@/api/client'
import { productKeys } from '@/api/products'
import type { components } from '@/api/schema'
import { PAGE_SIZE, type CatalogParams } from '@/features/catalog/catalog-params'

export type ProductPage = components['schemas']['PagedResponseOfProductResponse']

// GET /api/v1/products (EP-22) through the typed client; the BFF adds the bearer token.
// Search, price range, in-stock, sort and paging all go to the API; nothing is filtered in the browser.
export function productsQuery(params: CatalogParams) {
  return queryOptions({
    queryKey: productKeys.list(params),
    queryFn: ({ signal }): Promise<ProductPage> =>
      unwrap<ProductPage>(
        api.GET('/api/v1/products', {
          params: {
            query: {
              page: params.page,
              pageSize: PAGE_SIZE,
              sort: params.sort,
              search: params.search,
              minPrice: params.minPrice,
              maxPrice: params.maxPrice,
              inStock: params.inStock ? true : undefined,
            },
          },
          signal,
        }),
      ),
    // The previous page stays visible while the next one loads.
    placeholderData: keepPreviousData,
  })
}
