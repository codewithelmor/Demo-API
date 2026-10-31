import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api, unwrap } from '@/api/client'
import { orderKeys } from '@/api/orders'
import type { components } from '@/api/schema'
import { ORDERS_PAGE_SIZE, type OrdersParams } from '@/features/orders/orders-params'
import { localDayStartIso } from '@/lib/dates'

/** One page of the customer's order list. */
export type OrderPage = components['schemas']['PagedResponseOfMyOrderListItem']

/**
 * GET /api/v1/customers/me/orders (EP-46). Status, dates, sort and paging all go to the API.
 * The user picks whole days in their own time zone: From is the start of that day, and To (exclusive in
 * the API) is the start of the day after the chosen one.
 */
export function myOrdersQuery(params: OrdersParams) {
  return queryOptions({
    queryKey: orderKeys.list(params),
    queryFn: ({ signal }): Promise<OrderPage> =>
      unwrap<OrderPage>(
        api.GET('/api/v1/customers/me/orders', {
          params: {
            query: {
              page: params.page,
              pageSize: ORDERS_PAGE_SIZE,
              sort: params.sort,
              status: params.statuses.length > 0 ? params.statuses : undefined,
              createdFrom: params.from ? localDayStartIso(params.from) : undefined,
              createdTo: params.to ? localDayStartIso(params.to, 1) : undefined,
            },
          },
          signal,
        }),
      ),
    placeholderData: keepPreviousData,
  })
}
