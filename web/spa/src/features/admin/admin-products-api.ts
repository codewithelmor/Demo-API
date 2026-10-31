/** Admin product endpoints: list incl. deleted (EP-22), read (EP-23), create (EP-24), replace (EP-25), delete (EP-26), restore (EP-27). */
import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api, expectOk, unwrap, unwrapWithResponse } from '@/api/client'
import { productKeys, type Product } from '@/api/products'
import type { components } from '@/api/schema'
import { ADMIN_PAGE_SIZE, type AdminProductParams } from '@/features/admin/admin-products-params'

/** One page of the admin product list. */
export type AdminProductPage = components['schemas']['PagedResponseOfProductResponse']

/** A product and the ETag that came with it (sent back as If-Match when saving). */
export interface AdminProductData {
  product: Product
  etag: string | undefined
}

/** What the product form sends. The API replaces the whole product on update. */
export interface ProductInput {
  name: string
  sku: string
  description: string
  price: number
  stockQuantity: number
}

/** GET /api/v1/products with `includeDeleted` (EP-22). Filtering, sorting and paging all happen on the server. */
export function adminProductsQuery(params: AdminProductParams) {
  return queryOptions({
    queryKey: productKeys.list({ admin: true, ...params }),
    queryFn: ({ signal }): Promise<AdminProductPage> =>
      unwrap<AdminProductPage>(
        api.GET('/api/v1/products', {
          params: {
            query: {
              page: params.page,
              pageSize: ADMIN_PAGE_SIZE,
              sort: params.sort,
              search: params.search,
              includeDeleted: params.includeDeleted ? true : undefined,
            },
          },
          signal,
        }),
      ),
    placeholderData: keepPreviousData,
  })
}

/** GET /api/v1/products/{id} (EP-23) with the ETag. Admins may read a deleted product with `includeDeleted`. */
export function adminProductQuery(productId: string) {
  return queryOptions({
    queryKey: [...productKeys.detail(productId), 'admin'] as const,
    queryFn: async ({ signal }): Promise<AdminProductData> => {
      const { data, response } = await unwrapWithResponse<Product>(
        api.GET('/api/v1/products/{productId}', {
          params: { path: { productId }, query: { includeDeleted: true } },
          signal,
        }),
      )
      return { product: data, etag: response.headers.get('ETag') ?? undefined }
    },
  })
}

/** Turns the form values into the request body. An empty description is sent as null. */
function toBody(input: ProductInput) {
  return {
    name: input.name,
    sku: input.sku,
    description: input.description === '' ? null : input.description,
    price: input.price,
    stockQuantity: input.stockQuantity,
  }
}

/** POST /api/v1/products (EP-24). 409 `duplicate-sku` when an active product has the SKU. */
export async function createProduct(input: ProductInput): Promise<AdminProductData> {
  const { data, response } = await unwrapWithResponse<Product>(
    api.POST('/api/v1/products', { body: toBody(input) }),
  )
  return { product: data, etag: response.headers.get('ETag') ?? undefined }
}

/** PUT /api/v1/products/{id} (EP-25). Needs If-Match: 428 when missing, 412 when stale. */
export async function updateProduct(
  productId: string,
  etag: string | undefined,
  input: ProductInput,
): Promise<AdminProductData> {
  const { data, response } = await unwrapWithResponse<Product>(
    api.PUT('/api/v1/products/{productId}', {
      params: { path: { productId }, header: etag === undefined ? {} : { 'If-Match': etag } },
      body: toBody(input),
    }),
  )
  return { product: data, etag: response.headers.get('ETag') ?? undefined }
}

/** DELETE /api/v1/products/{id} (EP-26). A soft delete: existing order lines keep their product. */
export function deleteProduct(productId: string): Promise<void> {
  return expectOk(api.DELETE('/api/v1/products/{productId}', { params: { path: { productId } } }))
}

/** POST /api/v1/products/{id}/restore (EP-27). 409 `not-deleted` or `duplicate-sku`. */
export function restoreProduct(productId: string): Promise<void> {
  return expectOk(
    api.POST('/api/v1/products/{productId}/restore', { params: { path: { productId } } }),
  )
}
