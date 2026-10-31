import { useCartContext } from '@/hooks/cart-context'
import { useSession } from '@/lib/session'
import { addLines, linesOf, type AddResult, type CartProduct } from '@/lib/cart-reducer'
import { cartKeyFor, MAX_LINES, MAX_QUANTITY, type CartLine } from '@/lib/cart-storage'

export type { AddResult, CartProduct }

/** One shared empty list, so "no cart" is always the same object (React compares by identity). */
const EMPTY: readonly CartLine[] = []

/** The text for a cart change (shown in a `role="status"` region, so screen readers announce it). */
export function addMessage(result: AddResult, name: string, productCount: number): string {
  switch (result) {
    case 'added':
      return `Added ${name} to cart. ${String(productCount)} ${productCount === 1 ? 'product' : 'products'} in cart.`
    case 'cart-full':
      return `Max ${String(MAX_LINES)} products per order. Place this order or remove a product.`
    case 'max-quantity':
      return `You can order at most ${String(MAX_QUANTITY)} of one product.`
    case 'stock-limit':
      return 'That is all we have in stock.'
    case 'no-cart':
      return 'Only customers have a cart.'
  }
}

/**
 * React hook for the signed-in customer's cart. Only customers have one (it is keyed by the customer ID).
 * A session that ends keeps the cart (UX-D2); only sign out and delete account clear it.
 *
 * Returns:
 * - `lines`: the current cart lines (the component re-renders when they change, even from another tab).
 * - `count`: number of different products in the cart.
 * - `enabled`: false for users without a cart (for example Admins).
 * - `add`, `setQuantity`, `remove`, `restore`, `clear`: the ways to change the cart.
 */
export function useCart() {
  const user = useSession()
  const { state, getLines, send } = useCartContext()
  const key = user?.customerId ? cartKeyFor(user.customerId) : undefined
  const lines = key ? linesOf(state, key) : EMPTY

  return {
    lines,
    count: lines.length,
    enabled: key !== undefined,
    /** Adds `quantity` of a product, respecting the line, quantity and stock limits. Says what happened. */
    add: (product: CartProduct, quantity = 1): AddResult => {
      if (!key) return 'no-cart'
      // Check against the newest lines, so two quick adds in one event both count.
      const { result } = addLines(getLines(key), product, quantity)
      if (result === 'added') send({ type: 'add', key, product, quantity })
      return result
    },
    /** Sets the quantity of a line to an exact number. */
    setQuantity: (productId: string, quantity: number) => {
      if (key) send({ type: 'setQuantity', key, productId, quantity })
    },
    /** Removes a product's line. */
    remove: (productId: string) => {
      if (key) send({ type: 'remove', key, productId })
    },
    /** Puts a removed line back (Undo). */
    restore: (line: CartLine) => {
      if (key) send({ type: 'restore', key, line })
    },
    /** Empties the cart. */
    clear: () => {
      if (key) send({ type: 'clear', key })
    },
  }
}
