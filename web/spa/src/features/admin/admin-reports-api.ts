/** Report endpoints used by the admin landing page: low stock (EP-53) and sales (EP-52). */
import { queryOptions } from '@tanstack/react-query'
import { api, unwrap } from '@/api/client'
import type { components } from '@/api/schema'

export type LowStockPage = components['schemas']['PagedResponseOfLowStockItem']
export type SalesReport = components['schemas']['SalesReportResponse']

/** Days of sales the landing page shows. */
export const SALES_DAYS = 30

/** Cache keys for report data. */
export const reportKeys = {
  lowStock: ['reports', 'low-stock'] as const,
  sales: (days: number) => ['reports', 'sales', days] as const,
}

/**
 * Low-stock products (API default threshold). `pageSize: 1` because the landing page only needs the
 * count (`totalCount`), not the rows.
 */
export const lowStockQuery = queryOptions({
  queryKey: reportKeys.lowStock,
  queryFn: ({ signal }): Promise<LowStockPage> =>
    unwrap<LowStockPage>(
      api.GET('/api/v1/reports/low-stock', {
        params: { query: { page: 1, pageSize: 1 } },
        signal,
      }),
    ),
})

/** Sales of the last 30 days, grouped by month so the response stays small; only `totals` is shown. */
export function salesQuery(now: Date = new Date()) {
  const to = new Date(now)
  const from = new Date(now)
  from.setUTCDate(from.getUTCDate() - SALES_DAYS)
  return queryOptions({
    queryKey: reportKeys.sales(SALES_DAYS),
    queryFn: ({ signal }): Promise<SalesReport> =>
      unwrap<SalesReport>(
        api.GET('/api/v1/reports/sales', {
          params: {
            query: { groupBy: 'month', from: from.toISOString(), to: to.toISOString() },
          },
          signal,
        }),
      ),
  })
}
