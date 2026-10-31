import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router'
import { orderKeys, placeOrder, type OrderItemInput } from '@/api/orders'
import { productKeys } from '@/api/products'
import { Button } from '@/components/ui/button'
import { useCart } from '@/hooks/use-cart'
import { useCountdown } from '@/hooks/use-countdown'
import type { CartLine } from '@/lib/cart-storage'
import { ApiError, networkError } from '@/lib/problem-details'
import { messageFor, referenceFor } from '@/lib/problem-messages'

// Why a line is marked after a failed order: 'stock' = insufficient-stock names it, 'removed' = the
// API no longer knows the product (400 items[i].productId).
export type LineConflict = 'stock' | 'removed'

interface PlaceOrderPanelProps {
  lines: readonly CartLine[]
  // Set while the order cannot be placed (unavailable lines); shown next to the button.
  blockedReason: string | undefined
  onConflicts: (conflicts: ReadonlyMap<string, LineConflict>) => void
}

const ITEM_PRODUCT_KEY = /^items\[(\d+)\]\.productid$/i

function removedProducts(error: ApiError, items: OrderItemInput[]): string[] {
  const ids: string[] = []
  for (const key of Object.keys(error.problem.errors ?? {})) {
    const index = ITEM_PRODUCT_KEY.exec(key)?.[1]
    const item = index === undefined ? undefined : items[Number(index)]
    if (item) ids.push(item.productId)
  }
  return ids
}

// US-F029. One POST per click: the button is disabled while it runs, and a failed POST is never
// retried automatically (there is no Idempotency-Key, so a retry could create a second order, NFR-F24).
export function PlaceOrderPanel({ lines, blockedReason, onConflicts }: PlaceOrderPanelProps) {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const cart = useCart()
  const [failure, setFailure] = useState<ApiError | undefined>()
  const rateLimited = failure?.status === 429 ? failure : undefined
  const secondsLeft = useCountdown(rateLimited, rateLimited?.retryAfterSeconds ?? 0)

  const mutation = useMutation({
    mutationFn: placeOrder,
    onSuccess: async (order) => {
      // The order exists now: show it and empty the cart at once. Nothing here waits for the network, so a
      // closed tab or a stalled refresh cannot leave the cart full and invite a second order (no Idempotency-Key).
      queryClient.setQueryData(orderKeys.detail(order.id), order)
      onConflicts(new Map())
      cart.clear()
      // Refresh what the order changed in the background.
      void queryClient.invalidateQueries({ queryKey: orderKeys.all, refetchType: 'none' })
      void queryClient.invalidateQueries({ queryKey: productKeys.all })
      await navigate(`/orders/${order.id}`, { replace: true, state: { placed: true } })
    },
    onError: (cause, items) => {
      const error = cause instanceof ApiError ? cause : networkError(cause)
      setFailure(error)
      const conflicts = new Map<string, LineConflict>()
      if (error.slug === 'insufficient-stock') {
        for (const id of error.problem.productIds ?? []) conflicts.set(id, 'stock')
      }
      if (error.status === 400) {
        for (const id of removedProducts(error, items)) conflicts.set(id, 'removed')
      }
      onConflicts(conflicts)
      // Stock or products changed: read them again so the lines show the truth.
      if (conflicts.size > 0) void queryClient.invalidateQueries({ queryKey: productKeys.all })
    },
  })

  function submit() {
    // The button is disabled while pending; this also stops a second call from the same event burst.
    if (mutation.isPending || blockedReason !== undefined) return
    setFailure(undefined)
    onConflicts(new Map())
    mutation.mutate(lines.map((line) => ({ productId: line.productId, quantity: line.quantity })))
  }

  const locked = rateLimited !== undefined && secondsLeft > 0

  return (
    <div className="grid gap-3">
      {failure && <FailureAlert error={failure} />}
      <div className="flex flex-wrap items-center gap-3">
        <Button
          type="button"
          disabled={mutation.isPending || locked || blockedReason !== undefined}
          aria-busy={mutation.isPending}
          onClick={submit}
        >
          {mutation.isPending ? 'Placing order…' : 'Place order'}
        </Button>
        {blockedReason && <p className="text-sm">{blockedReason}</p>}
        {locked && (
          <span aria-hidden="true" className="text-sm text-muted-foreground">
            You can try again in {secondsLeft} s
          </span>
        )}
      </div>
    </div>
  )
}

function FailureAlert({ error }: { error: ApiError }) {
  const reference = referenceFor(error)

  let body: ReactNode
  if (error.status === 0) {
    // No answer arrived, so the order may or may not exist (NFR-F24).
    body = (
      <p>
        We could not confirm that your order was placed. Check My orders before trying again.{' '}
        <Link to="/orders" className="underline underline-offset-4">
          My orders
        </Link>
      </p>
    )
  } else if (error.slug === 'too-many-pending-orders') {
    const max = error.problem.maxPending ?? 5
    body = (
      <p>
        You have {max} open orders. Wait for one to be confirmed or cancel one.{' '}
        <Link to="/orders" className="underline underline-offset-4">
          View my orders
        </Link>
      </p>
    )
  } else if (error.slug === 'insufficient-stock') {
    body = <p>Some products do not have enough stock. Change the quantity or remove them.</p>
  } else if (error.status === 400 && error.slug === 'validation-failed') {
    body = <p>Some products are no longer available. Remove them to continue.</p>
  } else {
    body = <p>{messageFor(error)}</p>
  }

  return (
    <div role="alert" className="rounded-md border border-destructive p-3 text-sm">
      {body}
      {reference && <p className="mt-1 text-muted-foreground">Reference: {reference}</p>}
    </div>
  )
}
