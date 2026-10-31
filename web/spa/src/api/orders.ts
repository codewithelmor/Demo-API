import { queryOptions } from '@tanstack/react-query'
import { api, expectOk, unwrap } from '@/api/client'
import type { components } from '@/api/schema'

export type MyOrder = components['schemas']['MyOrderResponse']
export type MyOrderListItem = components['schemas']['MyOrderListItem']
export type OrderStatus = components['schemas']['OrderStatus']

export const ORDER_STATUSES: readonly OrderStatus[] = [
  'Pending',
  'Confirmed',
  'Shipped',
  'Completed',
  'Cancelled',
]

// Shared by the cart (place order) and the orders pages, which cannot import each other.
export const orderKeys = {
  all: ['orders'] as const,
  list: (params: object) => [...orderKeys.all, 'list', params] as const,
  detail: (id: string) => [...orderKeys.all, 'detail', id] as const,
}

// GET /api/v1/customers/me/orders/{orderId} (EP-47). Another customer's order is a 404.
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

export interface OrderItemInput {
  productId: string
  quantity: number
}

// POST /api/v1/customers/me/orders (EP-45). Never retried: there is no Idempotency-Key (gap G6).
export function placeOrder(items: OrderItemInput[]): Promise<MyOrder> {
  return unwrap<MyOrder>(api.POST('/api/v1/customers/me/orders', { body: { items } }))
}

// POST /api/v1/customers/me/orders/{orderId}/cancel (EP-48). The reason is optional.
export function cancelOrder(orderId: string, reason?: string): Promise<void> {
  const trimmed = reason?.trim()
  return expectOk(
    api.POST('/api/v1/customers/me/orders/{orderId}/cancel', {
      params: { path: { orderId } },
      body: trimmed ? { reason: trimmed } : undefined,
    }),
  )
}
