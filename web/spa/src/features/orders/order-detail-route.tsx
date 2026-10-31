import { useQueries, useQuery, type QueryClient, type UseQueryResult } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useParams, type LoaderFunctionArgs } from 'react-router'
import { myOrderQuery, type MyOrder, type OrderStatus } from '@/api/orders'
import { productQuery, type Product } from '@/api/products'
import { Button, buttonVariants } from '@/components/ui/button'
import { CancelOrderDialog } from '@/features/orders/cancel-order-dialog'
import { shortId, StatusBadge } from '@/features/orders/status-badge'
import { formatDateTime, parseDate } from '@/lib/dates'
import { apiErrorMessage } from '@/lib/error-message'
import { formatMoneyIn } from '@/lib/money'
import { ApiError } from '@/lib/problem-details'

export function orderDetailLoader(queryClient: QueryClient) {
  return ({ params }: LoaderFunctionArgs) => {
    if (params.orderId) queryClient.query(myOrderQuery(params.orderId)).catch(() => undefined)
    return null
  }
}

// True when the page was opened right after placing the order (the cart passes it in the history state).
function cameFromCheckout(state: unknown): boolean {
  return typeof state === 'object' && state !== null && 'placed' in state && state.placed === true
}

// CU-5
export function OrderDetailRoute() {
  const { orderId = '' } = useParams()
  const location = useLocation()
  const { data, error, isPending, isError, refetch, isFetching } = useQuery(myOrderQuery(orderId))

  if (isPending) {
    return (
      <section className="grid gap-4">
        <title>Order · Orders Shop</title>
        <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
          Order
        </h1>
        <p role="status">Loading order…</p>
      </section>
    )
  }

  if (isError) {
    // Another customer's order is a 404 too, so it looks the same as one that does not exist.
    const notFound = error instanceof ApiError && error.status === 404
    return (
      <section className="grid max-w-prose justify-items-start gap-4">
        <title>{notFound ? 'Order not found' : 'Order'} · Orders Shop</title>
        <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
          {notFound ? 'Order not found' : 'We could not load this order'}
        </h1>
        {notFound ? (
          <p>Order not found.</p>
        ) : (
          <div role="alert" className="rounded-md border border-destructive p-3">
            <p>{apiErrorMessage(error)}</p>
          </div>
        )}
        <div className="flex gap-2">
          {!notFound && (
            <Button
              variant="outline"
              disabled={isFetching}
              onClick={() => {
                void refetch()
              }}
            >
              {isFetching ? 'Trying again…' : 'Try again'}
            </Button>
          )}
          <Link
            to="/orders"
            className={buttonVariants({ variant: notFound ? 'default' : 'outline' })}
          >
            Go to My orders
          </Link>
        </div>
      </section>
    )
  }

  return <OrderDetail order={data} placed={cameFromCheckout(location.state)} />
}

function statusText(status: OrderStatus): string {
  return status === 'Cancelled' ? 'Order cancelled.' : `Order status is now ${status}.`
}

// What the page shows for a line's product name (EP-23, one call per distinct product, kept for the session).
function productCell(query: UseQueryResult<Product> | undefined, productId: string) {
  if (!query || query.isPending) {
    return (
      <span aria-busy="true" className="text-muted-foreground">
        Loading name…
      </span>
    )
  }
  if (query.isError) {
    return query.error instanceof ApiError && query.error.status === 404 ? (
      <span>Product no longer available</span>
    ) : (
      <span className="text-muted-foreground">Name not available</span>
    )
  }
  return (
    <Link to={`/products/${productId}`} className="underline underline-offset-4">
      {query.data.name}
    </Link>
  )
}

function OrderDetail({ order, placed }: { order: MyOrder; placed: boolean }) {
  // Status changes are announced: after a cancel, or when the order moved on while the page was open.
  const [seenStatus, setSeenStatus] = useState(order.status)
  const [announcement, setAnnouncement] = useState('')
  if (order.status !== seenStatus) {
    setSeenStatus(order.status)
    setAnnouncement(statusText(order.status))
  }
  const [conflict, setConflict] = useState<string | undefined>()

  // Focus must not be lost (WCAG 2.4.3): when the loading page is replaced by the order, and when a
  // status change removes the button that had focus (Cancel order), it moves to the page heading.
  const headingRef = useRef<HTMLHeadingElement>(null)
  useEffect(() => {
    const active = document.activeElement
    if (!active || active === document.body) headingRef.current?.focus()
  }, [])
  useEffect(() => {
    if (announcement !== '') headingRef.current?.focus()
  }, [announcement])

  // Names: one call per distinct product, at most 50 (an order has at most 50 lines). A name does
  // not change, so it is kept for the whole session.
  const productIds = [...new Set(order.items.map((item) => item.productId))]
  const names = useQueries({
    queries: productIds.map((id) => ({ ...productQuery(id), staleTime: Infinity })),
  })
  const nameFor = (id: string) => names[productIds.indexOf(id)]

  const history = [...order.history].sort(
    (a, b) => (parseDate(b.at)?.getTime() ?? 0) - (parseDate(a.at)?.getTime() ?? 0),
  )
  const placedAt = formatDateTime(order.createdAt)
  const updatedAt = formatDateTime(order.lastModifiedAt)

  return (
    <section className="grid gap-4">
      <title>{`Order #${shortId(order.id)} · Orders Shop`}</title>
      <Link to="/orders" className="text-sm underline underline-offset-4">
        My orders
      </Link>
      <div className="flex flex-wrap items-center gap-3">
        <h1
          ref={headingRef}
          tabIndex={-1}
          className="font-mono text-2xl font-semibold outline-none"
        >
          Order #{shortId(order.id)}
        </h1>
        <StatusBadge status={order.status} />
      </div>
      <p className="text-sm text-muted-foreground">
        Placed {placedAt}
        {updatedAt && updatedAt !== placedAt ? `. Last updated ${updatedAt}` : ''}
      </p>

      {/* Always in the page, so status changes are announced. */}
      <p role="status" className="min-h-5 text-sm">
        {announcement}
      </p>
      {/* A region that is always in the page, so the text is announced; empty (and hidden) unless the order was just placed. */}
      <p role="status" className={placed ? 'rounded-md bg-muted p-3 text-sm' : 'sr-only'}>
        {placed ? 'Order placed. We will confirm it soon.' : ''}
      </p>
      {conflict && (
        <p role="alert" className="rounded-md border border-destructive p-3 text-sm">
          {conflict}
        </p>
      )}

      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <caption className="sr-only">Items in this order</caption>
          <thead>
            <tr className="border-b">
              <th scope="col" className="py-2 pr-4 font-medium">
                Product
              </th>
              <th scope="col" className="py-2 pr-4 text-right font-medium">
                Unit price
              </th>
              <th scope="col" className="py-2 pr-4 text-right font-medium">
                Qty
              </th>
              <th scope="col" className="py-2 text-right font-medium">
                Line total
              </th>
            </tr>
          </thead>
          <tbody>
            {order.items.map((item) => (
              <tr key={item.productId} className="border-b">
                <th scope="row" className="py-3 pr-4 font-normal">
                  {productCell(nameFor(item.productId), item.productId)}
                </th>
                <td className="py-3 pr-4 text-right">
                  {formatMoneyIn(item.unitPrice, order.currency)}
                </td>
                <td className="py-3 pr-4 text-right">{item.quantity}</td>
                <td className="py-3 text-right">{formatMoneyIn(item.lineTotal, order.currency)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="text-lg font-semibold">
        Total {formatMoneyIn(order.total, order.currency)} ({order.currency})
      </p>

      <div className="grid gap-2">
        <h2 className="text-lg font-medium">Status history</h2>
        <ol aria-label="Status history" className="grid gap-1 text-sm">
          {history.map((entry) => (
            <li key={`${String(entry.at)}-${entry.to}`}>
              {entry.from ? `${entry.from} to ${entry.to}` : `Placed as ${entry.to}`}
              {', '}
              {formatDateTime(entry.at)}
              {entry.reason ? `. Reason: ${entry.reason}` : ''}
            </li>
          ))}
        </ol>
      </div>

      {order.status === 'Pending' ? (
        <div>
          <CancelOrderDialog orderId={order.id} onConflict={setConflict} />
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">Only pending orders can be cancelled.</p>
      )}
    </section>
  )
}
