/**
 * Order endpoints of the API (the signed-in customer's own orders), plus the cache keys for them.
 * Types come from the generated OpenAPI schema, so they always match the server.
 */
import { queryOptions } from '@tanstack/react-query'
import { api, expectOk, unwrap } from '@/api/client'
import type { components } from '@/api/schema'

export type MyOrder = components['schemas']['MyOrderResponse']
export type MyOrderListItem = components['schemas']['MyOrderListItem']
export type OrderStatus = components['schemas']['OrderStatus']

/** Every status an order can have, in the order they usually happen (used for filters). */
export const ORDER_STATUSES: readonly OrderStatus[] = [
  'Pending',
  'Confirmed',
  'Shipped',
  'Completed',
  'Cancelled',
]

/**
 * Cache keys for order data. A key is an array that names a piece of cached data; TanStack Query uses
 * it to find, share and invalidate that data. Shared by the cart (place order) and the orders pages,
 * which cannot import each other.
 */
export const orderKeys = {
  all: ['orders'] as const,
  list: (params: object) => [...orderKeys.all, 'list', params] as const,
  detail: (id: string) => [...orderKeys.all, 'detail', id] as const,
}

/**
 * Query definition for one order: GET /api/v1/customers/me/orders/{orderId} (EP-47).
 * Another customer's order is a 404, never a 403.
 */
export function myOrderQuery(orderId: string) {
  return queryOptions({
    queryKey: orderKeys.detail(orderId),
    queryFn: ({ signal }): Promise<MyOrder> =>
      unwrap<MyOrder>(
        api.GET('/api/v1/customers/me/orders/{orderId}', {
          params: { path: { orderId } },
          signal,
        }),
      ),
  })
}

/** One line of a new order: which product and how many. */
export interface OrderItemInput {
  productId: string
  quantity: number
}

/**
 * Places an order: POST /api/v1/customers/me/orders (EP-45). Never retried automatically: the API has no
 * Idempotency-Key (gap G6), so a retry could create the order twice.
 */
export function placeOrder(items: OrderItemInput[]): Promise<MyOrder> {
  return unwrap<MyOrder>(api.POST('/api/v1/customers/me/orders', { body: { items } }))
}

/** Cancels an order: POST /api/v1/customers/me/orders/{orderId}/cancel (EP-48). The reason is optional. */
export function cancelOrder(orderId: string, reason?: string): Promise<void> {
  const trimmed = reason?.trim()
  return expectOk(
    api.POST('/api/v1/customers/me/orders/{orderId}/cancel', {
      params: { path: { orderId } },
      body: trimmed ? { reason: trimmed } : undefined,
    }),
  )
}
