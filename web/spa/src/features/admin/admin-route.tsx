/**
 * The back office landing page (FR-F050): the low-stock count and the last 30 days of sales.
 * This module is its own chunk (the router loads it with lazy()), and nothing outside features/admin imports it.
 * The two reports load side by side; the API allows 4 report calls at once (NFR-F27).
 */
import { useQuery, type QueryClient } from '@tanstack/react-query'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { lowStockQuery, SALES_DAYS, salesQuery } from '@/features/admin/admin-reports-api'
import { apiErrorMessage } from '@/lib/error-message'
import { formatMoney } from '@/lib/money'

/** Starts both report requests before the page renders; the page shows its own loading and error states. */
export function adminLoader(queryClient: QueryClient) {
  return () => {
    queryClient.query(lowStockQuery).catch(() => undefined)
    queryClient.query(salesQuery()).catch(() => undefined)
    return null
  }
}

/** A titled number box. */
function Stat({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="rounded-lg border p-4">
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="mt-1 text-2xl font-semibold">{children}</dd>
    </div>
  )
}

/** Shows a failed report with a retry button, so one failing report does not hide the other. */
function ReportError({ error, retry, busy }: { error: unknown; retry: () => void; busy: boolean }) {
  return (
    <div
      role="alert"
      className="grid justify-items-start gap-3 rounded-md border border-destructive p-4"
    >
      <p>{apiErrorMessage(error)}</p>
      <Button variant="outline" disabled={busy} onClick={retry}>
        {busy ? 'Trying again…' : 'Try again'}
      </Button>
    </div>
  )
}

/** The back office dashboard. */
export function AdminRoute() {
  const lowStock = useQuery(lowStockQuery)
  const sales = useQuery(salesQuery())

  return (
    <section className="grid gap-4">
      <title>Back office · Orders Shop</title>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        Back office
      </h1>

      <h2 className="text-lg font-medium">Stock</h2>
      {lowStock.isPending && <p role="status">Loading stock…</p>}
      {lowStock.isError && (
        <ReportError
          error={lowStock.error}
          busy={lowStock.isFetching}
          retry={() => {
            void lowStock.refetch()
          }}
        />
      )}
      {lowStock.data && (
        <dl>
          <Stat label="Products low on stock">{lowStock.data.totalCount}</Stat>
        </dl>
      )}

      <h2 className="text-lg font-medium">Sales, last {SALES_DAYS} days</h2>
      {sales.isPending && <p role="status">Loading sales…</p>}
      {sales.isError && (
        <ReportError
          error={sales.error}
          busy={sales.isFetching}
          retry={() => {
            void sales.refetch()
          }}
        />
      )}
      {sales.data && (
        <dl className="grid gap-4 sm:grid-cols-3">
          <Stat label="Orders">{sales.data.totals.orderCount}</Stat>
          <Stat label="Units sold">{sales.data.totals.units}</Stat>
          <Stat label="Revenue">{formatMoney(sales.data.totals.revenue)}</Stat>
        </dl>
      )}

      <p>
        <Link to="/admin/products" className="underline">
          Manage products
        </Link>
      </p>
    </section>
  )
}
