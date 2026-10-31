import { useQuery, type QueryClient } from '@tanstack/react-query'
import { Link, useSearchParams, type LoaderFunctionArgs } from 'react-router'
import { Button, buttonVariants } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { NativeSelect } from '@/components/ui/native-select'
import { apiErrorMessage } from '@/lib/error-message'
import { myOrdersQuery } from '@/features/orders/orders-api'
import { OrdersFilters } from '@/features/orders/orders-filters'
import {
  hasOrderFilters,
  ORDER_SORT_LABELS,
  ORDER_SORTS,
  parseOrdersParams,
  toOrdersSearchParams,
  type OrderSort,
  type OrdersParams,
} from '@/features/orders/orders-params'
import { shortId, StatusBadge } from '@/features/orders/status-badge'
import { formatDateTime } from '@/lib/dates'
import { formatMoneyIn } from '@/lib/money'

export function ordersLoader(queryClient: QueryClient) {
  return ({ request }: LoaderFunctionArgs) => {
    const params = parseOrdersParams(new URL(request.url).searchParams)
    // Not awaited: the page renders its loading state while the request runs.
    queryClient.query(myOrdersQuery(params)).catch(() => undefined)
    return null
  }
}

function isSort(value: string): value is OrderSort {
  return ORDER_SORTS.some((sort) => sort === value)
}

// CU-4. Status, dates, sort and page are in the URL and are sent to the API.
export function OrdersRoute() {
  const [searchParams, setSearchParams] = useSearchParams()
  const params = parseOrdersParams(searchParams)
  const { data, error, isPending, isError, refetch, isFetching } = useQuery(myOrdersQuery(params))

  function update(change: (current: OrdersParams) => OrdersParams) {
    setSearchParams((previous) => toOrdersSearchParams(change(parseOrdersParams(previous))))
  }

  function clearFilters() {
    update((current) => ({
      ...current,
      statuses: [],
      from: undefined,
      to: undefined,
      page: 1,
    }))
  }

  const filtersActive = hasOrderFilters(params)
  const totalPages = Math.max(data?.totalPages ?? 1, 1)
  const pageLink = (page: number) => {
    const query = toOrdersSearchParams({ ...params, page }).toString()
    return query === '' ? '/orders' : `/orders?${query}`
  }

  return (
    <section className="grid gap-4">
      <title>My orders · Orders Shop</title>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
          My orders
        </h1>
        {/* Always in the page: a new count is announced when filters, sort or paging change the list. */}
        <p role="status" className="min-h-5 text-sm text-muted-foreground">
          {data && data.totalCount > 0
            ? `${String(data.totalCount)} ${data.totalCount === 1 ? 'order' : 'orders'}`
            : ''}
        </p>
      </div>

      <OrdersFilters
        key={`${params.statuses.join(',')}|${params.from ?? ''}|${params.to ?? ''}`}
        params={params}
        canClear={filtersActive}
        onApply={(filters) => {
          update((current) => ({ ...current, ...filters, page: 1 }))
        }}
        onClear={clearFilters}
      />

      <div className="grid w-52 gap-1.5">
        <Label htmlFor="orders-sort">Sort</Label>
        <NativeSelect
          id="orders-sort"
          value={params.sort}
          onChange={(event) => {
            const sort = event.target.value
            if (isSort(sort)) update((current) => ({ ...current, sort, page: 1 }))
          }}
        >
          {ORDER_SORTS.map((sort) => (
            <option key={sort} value={sort}>
              {ORDER_SORT_LABELS[sort]}
            </option>
          ))}
        </NativeSelect>
      </div>

      {isPending && (
        <>
          <p role="status" className="sr-only">
            Loading orders…
          </p>
          <div aria-hidden="true" className="grid gap-2">
            {Array.from({ length: 5 }, (_, index) => (
              <div key={index} className="h-10 animate-pulse rounded-md bg-muted" />
            ))}
          </div>
        </>
      )}

      {isError && (
        <div
          role="alert"
          className="grid justify-items-start gap-3 rounded-md border border-destructive p-4"
        >
          <p>{apiErrorMessage(error)}</p>
          <Button
            variant="outline"
            disabled={isFetching}
            onClick={() => {
              void refetch()
            }}
          >
            {isFetching ? 'Trying again…' : 'Try again'}
          </Button>
        </div>
      )}

      {data && data.items.length === 0 && (
        <div className="grid justify-items-start gap-3">
          {params.page > 1 ? (
            <>
              <p>No orders on this page.</p>
              <Link to={pageLink(1)} className={buttonVariants({ variant: 'outline' })}>
                Go to page 1
              </Link>
            </>
          ) : filtersActive ? (
            <>
              <p>No orders match these filters.</p>
              <Button type="button" variant="outline" onClick={clearFilters}>
                Clear filters
              </Button>
            </>
          ) : (
            <>
              <p>You have not placed any orders yet.</p>
              <Link to="/products" className={buttonVariants()}>
                Browse products
              </Link>
            </>
          )}
        </div>
      )}

      {data && data.items.length > 0 && (
        <div className="overflow-x-auto" aria-busy={isFetching}>
          <table className="w-full text-left text-sm">
            <caption className="sr-only">Your orders</caption>
            <thead>
              <tr className="border-b">
                <th scope="col" className="py-2 pr-4 font-medium">
                  Order
                </th>
                <th scope="col" className="py-2 pr-4 font-medium">
                  Date
                </th>
                <th scope="col" className="py-2 pr-4 font-medium">
                  Status
                </th>
                <th scope="col" className="py-2 text-right font-medium">
                  Total
                </th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((order) => (
                <tr key={order.id} className="border-b">
                  <th scope="row" className="py-3 pr-4 font-normal">
                    <Link
                      to={`/orders/${order.id}`}
                      className="font-mono underline underline-offset-4"
                      aria-label={`Order ${shortId(order.id)}`}
                    >
                      #{shortId(order.id)}
                    </Link>
                  </th>
                  <td className="py-3 pr-4">{formatDateTime(order.createdAt)}</td>
                  <td className="py-3 pr-4">
                    <StatusBadge status={order.status} />
                  </td>
                  <td className="py-3 text-right">{formatMoneyIn(order.total, order.currency)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {data && data.totalCount > 0 && (
        <nav aria-label="Pagination" className="flex items-center gap-3">
          {params.page > 1 ? (
            <Link to={pageLink(params.page - 1)} className={buttonVariants({ variant: 'outline' })}>
              Previous
            </Link>
          ) : null}
          <span className="text-sm">
            Page {params.page} of {totalPages}
          </span>
          {params.page < totalPages ? (
            <Link to={pageLink(params.page + 1)} className={buttonVariants({ variant: 'outline' })}>
              Next
            </Link>
          ) : null}
        </nav>
      )}
    </section>
  )
}
