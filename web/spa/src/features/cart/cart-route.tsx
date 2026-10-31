import { useQueries, type UseQueryResult } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { Link } from 'react-router'
import { productQuery, type Product } from '@/api/products'
import { QuantityInput } from '@/components/quantity-input'
import { Badge } from '@/components/ui/badge'
import { Button, buttonVariants } from '@/components/ui/button'
import { PlaceOrderPanel, type LineConflict } from '@/features/cart/place-order-panel'
import { useCart } from '@/hooks/use-cart'
import { MAX_LINES, MAX_QUANTITY, type CartLine } from '@/lib/cart-storage'
import { formatMoney } from '@/lib/money'
import { ApiError } from '@/lib/problem-details'
import { messageFor } from '@/lib/problem-messages'

// What the cart page knows about a line after re-reading its product (EP-23).
type LineState =
  | { kind: 'loading' }
  | { kind: 'gone' } // 404: the product was deleted
  | { kind: 'error'; error: ApiError }
  | { kind: 'out-of-stock'; product: Product }
  | { kind: 'low-stock'; product: Product }
  | { kind: 'ok'; product: Product }

function stateOf(line: CartLine, query: UseQueryResult<Product>): LineState {
  if (query.isError) {
    return query.error instanceof ApiError && query.error.status === 404
      ? { kind: 'gone' }
      : {
          kind: 'error',
          error:
            query.error instanceof ApiError
              ? query.error
              : new ApiError({ title: 'Something went wrong.', status: 0 }),
        }
  }
  const product = query.data
  if (!product) return { kind: 'loading' }
  if (product.stockQuantity <= 0) return { kind: 'out-of-stock', product }
  if (product.stockQuantity < line.quantity) return { kind: 'low-stock', product }
  return { kind: 'ok', product }
}

const hasProblem = (state: LineState) => state.kind !== 'ok' && state.kind !== 'loading'

// Why Place order is off (wireframe CU-3), or undefined when it is on.
function blockReason(states: LineState[]): string | undefined {
  if (states.some((state) => state.kind === 'loading')) return 'Loading your products…'
  if (states.some((state) => ['gone', 'error', 'out-of-stock'].includes(state.kind))) {
    return 'Remove unavailable products to continue.'
  }
  if (states.some((state) => state.kind === 'low-stock')) {
    return 'Change the quantity of the marked products to continue.'
  }
  return undefined
}

// Estimated total in cents, so adding prices never drifts (the final total comes from the order).
function lineCents(price: number, quantity: number): number {
  return Math.round(price * 100) * quantity
}

// CU-3. Add, change and remove lines. Each product is read again on open, so prices and stock are current.
export function CartRoute() {
  const cart = useCart()
  const headingRef = useRef<HTMLHeadingElement>(null)
  const [removed, setRemoved] = useState<{ line: CartLine; name: string } | undefined>()
  // Lines the last failed order named (insufficient-stock, product removed).
  const [conflicts, setConflicts] = useState<ReadonlyMap<string, LineConflict>>(new Map())

  const results = useQueries({
    queries: cart.lines.map((line) => ({
      ...productQuery(line.productId),
      refetchOnMount: 'always' as const,
    })),
  })

  const rows = cart.lines
    .map((line, index) => {
      const query = results[index]
      const conflict = conflicts.get(line.productId)
      const read: LineState = query ? stateOf(line, query) : { kind: 'loading' }
      // The API said the product is gone: treat it as removed even if our copy is still cached.
      const state: LineState = conflict === 'removed' ? { kind: 'gone' } : read
      return { line, state, conflict }
    })
    // Lines with problems first.
    .sort(
      (a, b) =>
        Number(hasProblem(b.state) || b.conflict !== undefined) -
        Number(hasProblem(a.state) || a.conflict !== undefined),
    )

  const blockedReason = blockReason(rows.map((row) => row.state))

  const totalCents = rows.reduce(
    (sum, { line, state }) =>
      state.kind === 'ok' ? sum + lineCents(state.product.price, line.quantity) : sum,
    0,
  )

  function clearConflict(productId: string) {
    if (!conflicts.has(productId)) return
    const next = new Map(conflicts)
    next.delete(productId)
    setConflicts(next)
  }

  function remove(line: CartLine, name: string) {
    clearConflict(line.productId)
    cart.remove(line.productId)
    setRemoved({ line, name })
  }

  return (
    <section className="grid gap-4">
      <title>Cart · Orders Shop</title>
      <h1 ref={headingRef} tabIndex={-1} className="text-2xl font-semibold outline-none">
        Cart ({cart.count} {cart.count === 1 ? 'product' : 'products'})
      </h1>

      {/* Always in the page, so a removal is announced. */}
      <div role="status" className="min-h-6 text-sm">
        {removed && (
          <p className="flex flex-wrap items-center gap-2">
            Removed {removed.name}.
            <Button
              type="button"
              size="sm"
              variant="outline"
              // The button appears when a line is removed: focus moves to it, so it is not lost with the row.
              ref={(element) => {
                element?.focus()
              }}
              onClick={() => {
                cart.restore(removed.line)
                setRemoved(undefined)
                headingRef.current?.focus()
              }}
            >
              Undo
            </Button>
          </p>
        )}
      </div>

      {cart.count === 0 ? (
        <div className="grid justify-items-start gap-3">
          <p>Your cart is empty. Browse products to add some.</p>
          <Link to="/products" className={buttonVariants()}>
            Browse products
          </Link>
        </div>
      ) : (
        <>
          <ul className="grid gap-3">
            {rows.map(({ line, state, conflict }) => (
              <CartRow
                key={line.productId}
                line={line}
                state={state}
                conflict={conflict}
                onQuantity={(quantity) => {
                  clearConflict(line.productId)
                  cart.setQuantity(line.productId, quantity)
                }}
                onRemove={(name) => {
                  remove(line, name)
                }}
              />
            ))}
          </ul>

          <div className="grid gap-1 rounded-lg border p-4">
            <p className="text-lg font-semibold">Estimated total {formatMoney(totalCents / 100)}</p>
            <p className="text-sm text-muted-foreground">
              Final total is confirmed when you place the order.
            </p>
          </div>
          {cart.count >= MAX_LINES && (
            <p className="text-sm">
              Your cart has {MAX_LINES} products, the most for one order. Max {MAX_LINES} products
              per order.
            </p>
          )}
          <PlaceOrderPanel
            lines={cart.lines}
            blockedReason={blockedReason}
            onConflicts={setConflicts}
          />
          <div>
            <Link to="/products" className={buttonVariants({ variant: 'outline' })}>
              Continue shopping
            </Link>
          </div>
        </>
      )}
    </section>
  )
}

interface CartRowProps {
  line: CartLine
  state: LineState
  conflict: LineConflict | undefined
  onQuantity: (quantity: number) => void
  onRemove: (name: string) => void
}

function CartRow({ line, state, conflict, onQuantity, onRemove }: CartRowProps) {
  if (state.kind === 'loading') {
    return (
      <li className="rounded-lg border p-4 text-sm text-muted-foreground" aria-busy="true">
        Loading product…
      </li>
    )
  }

  if (state.kind === 'gone' || state.kind === 'error') {
    return (
      <li className="grid justify-items-start gap-2 rounded-lg border p-4">
        <p className="font-medium">Product</p>
        <p role="alert" className="text-sm text-destructive">
          {state.kind === 'gone' ? 'No longer available.' : messageFor(state.error)}
        </p>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => {
            onRemove('product')
          }}
        >
          Remove
        </Button>
      </li>
    )
  }

  const { product } = state
  const priceChanged = product.price !== line.price
  const max = Math.min(MAX_QUANTITY, Math.max(product.stockQuantity, 1))

  return (
    <li className="grid gap-3 rounded-lg border p-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="text-lg font-medium">
          <Link to={`/products/${product.id}`} className="underline-offset-4 hover:underline">
            {product.name}
          </Link>
        </h2>
        <p className="font-semibold">{formatMoney(product.price)}</p>
      </div>
      {priceChanged && (
        <div>
          <Badge variant="muted">Price updated (was {formatMoney(line.price)})</Badge>
        </div>
      )}

      {state.kind === 'out-of-stock' && (
        <p role="alert" className="text-sm text-destructive">
          Out of stock. Remove it to continue.
        </p>
      )}
      {conflict === 'stock' && state.kind !== 'low-stock' && state.kind !== 'out-of-stock' && (
        <p role="alert" className="text-sm text-destructive">
          Not enough stock for this quantity. Change the quantity or remove it.
        </p>
      )}
      {state.kind === 'low-stock' && (
        <div role="alert" className="flex flex-wrap items-center gap-2 text-sm text-destructive">
          <span>Only {product.stockQuantity} left.</span>
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={() => {
              onQuantity(product.stockQuantity)
            }}
          >
            Set to {product.stockQuantity}
          </Button>
        </div>
      )}

      <div className="flex flex-wrap items-end justify-between gap-3">
        {state.kind !== 'out-of-stock' && (
          <QuantityInput
            label={`Quantity of ${product.name}`}
            value={line.quantity}
            max={max}
            onChange={onQuantity}
          />
        )}
        <div className="flex items-center gap-3">
          <p className="font-medium">
            {state.kind === 'out-of-stock'
              ? ''
              : formatMoney(lineCents(product.price, line.quantity) / 100)}
          </p>
          <Button
            type="button"
            variant="outline"
            size="sm"
            aria-label={`Remove ${product.name}`}
            onClick={() => {
              onRemove(product.name)
            }}
          >
            Remove
          </Button>
        </div>
      </div>
    </li>
  )
}
