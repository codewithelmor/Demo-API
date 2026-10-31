import { useSyncExternalStore } from 'react'
import { useSession } from '@/lib/session'
import {
  cartKeyFor,
  clearCart,
  MAX_LINES,
  MAX_QUANTITY,
  readCart,
  subscribeCart,
  writeCart,
  type CartLine,
} from '@/lib/cart-storage'

/** One shared empty list, so "no cart" is always the same object (React compares by identity). */
const EMPTY: readonly CartLine[] = []

/** Outcome of trying to add a product to the cart. */
export type AddResult = 'added' | 'cart-full' | 'max-quantity' | 'stock-limit' | 'no-cart'

/** The facts about a product the cart needs when adding it. */
export interface CartProduct {
  id: string
  price: number
  stockQuantity: number
}

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
  const key = user?.customerId ? cartKeyFor(user.customerId) : undefined
  const lines = useSyncExternalStore(subscribeCart, () => (key ? readCart(key) : EMPTY))

  /** Stores a new list of lines (nothing happens when the user has no cart). */
  function save(next: readonly CartLine[]) {
    if (key) writeCart(key, next)
  }

  // Each action reads the stored cart when it runs, not the snapshot of the last render, so two quick
  // actions (or another tab) never overwrite each other's change.
  /** Adds `quantity` of a product, respecting the line, quantity and stock limits. Says what happened. */
  function add(product: CartProduct, quantity = 1): AddResult {
    if (!key) return 'no-cart'
    const lines = readCart(key)
    const existing = lines.find((line) => line.productId === product.id)
    if (!existing && lines.length >= MAX_LINES) return 'cart-full'
    const total = (existing?.quantity ?? 0) + quantity
    if (total > MAX_QUANTITY) return 'max-quantity'
    if (total > product.stockQuantity) return 'stock-limit'
    // The price seen is the one on the screen when the customer added it.
    const line: CartLine = { productId: product.id, quantity: total, price: product.price }
    save(existing ? lines.map((l) => (l.productId === product.id ? line : l)) : [...lines, line])
    return 'added'
  }

  /** Sets the quantity of a line to an exact number. */
  function setQuantity(productId: string, quantity: number) {
    const lines = key ? readCart(key) : EMPTY
    save(lines.map((line) => (line.productId === productId ? { ...line, quantity } : line)))
  }

  /** Removes a product's line. */
  function remove(productId: string) {
    const lines = key ? readCart(key) : EMPTY
    save(lines.filter((line) => line.productId !== productId))
  }

  /** Puts a removed line back (Undo). */
  function restore(line: CartLine) {
    const lines = key ? readCart(key) : EMPTY
    if (lines.length < MAX_LINES && !lines.some((l) => l.productId === line.productId)) {
      save([...lines, line])
    }
  }

  /** Empties the cart. */
  function clear() {
    if (key) clearCart(key)
  }

  return {
    lines,
    count: lines.length,
    enabled: key !== undefined,
    add,
    setQuantity,
    remove,
    restore,
    clear,
  }
}
