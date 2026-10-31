import { z } from '@/lib/zod'
import { ORDER_STATUSES, type OrderStatus } from '@/api/orders'
import { isValidDay } from '@/lib/dates'

// My orders: status (repeatable), From, To, sort and page live in the URL (information architecture CU-4):
//   /orders?status=Pending&status=Shipped&from=2026-01-01&to=2026-01-31&sort=-total&page=2
// Bad values fall back to the default, so a hand-edited URL never breaks the page.

export const ORDERS_PAGE_SIZE = 20

// API sort values: createdAt (default -createdAt) and total.
export const ORDER_SORTS = ['-createdAt', 'createdAt', '-total', 'total'] as const
export type OrderSort = (typeof ORDER_SORTS)[number]

export const ORDER_SORT_LABELS: Record<OrderSort, string> = {
  '-createdAt': 'Newest first',
  createdAt: 'Oldest first',
  '-total': 'Total high to low',
  total: 'Total low to high',
}

const day = z.string().refine(isValidDay).optional().catch(undefined)

const paramsSchema = z.object({
  from: day,
  to: day,
  sort: z.enum(ORDER_SORTS).catch('-createdAt'),
  page: z.coerce.number().int().min(1).catch(1),
})

export interface OrdersParams {
  statuses: OrderStatus[]
  from: string | undefined
  to: string | undefined
  sort: OrderSort
  page: number
}

export function parseOrdersParams(searchParams: URLSearchParams): OrdersParams {
  const rest = paramsSchema.parse({
    from: searchParams.get('from') ?? undefined,
    to: searchParams.get('to') ?? undefined,
    sort: searchParams.get('sort') ?? undefined,
    page: searchParams.get('page') ?? undefined,
  })
  const wanted = new Set(searchParams.getAll('status'))
  const statuses = ORDER_STATUSES.filter((status) => wanted.has(status))
  // To before From would be a 400 from the API; the filter form never produces it.
  const to =
    rest.from !== undefined && rest.to !== undefined && rest.to < rest.from ? undefined : rest.to
  return { statuses, from: rest.from, to, sort: rest.sort, page: rest.page }
}

// Only values that differ from the defaults go into the URL.
export function toOrdersSearchParams(params: OrdersParams): URLSearchParams {
  const result = new URLSearchParams()
  for (const status of params.statuses) result.append('status', status)
  if (params.from) result.set('from', params.from)
  if (params.to) result.set('to', params.to)
  if (params.sort !== '-createdAt') result.set('sort', params.sort)
  if (params.page > 1) result.set('page', String(params.page))
  return result
}

export function hasOrderFilters(params: OrdersParams): boolean {
  return params.statuses.length > 0 || params.from !== undefined || params.to !== undefined
}
