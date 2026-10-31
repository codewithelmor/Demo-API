import { createContext, use } from 'react'
import type { CartLine } from '@/lib/cart-storage'
import type { CartAction, CartState } from '@/lib/cart-reducer'

/** What `CartProvider` shares with the components below it. */
export interface CartContextValue {
  /** The lines of a cart as they are right now (also right after a `send` in the same event). */
  getLines: (key: string) => readonly CartLine[]
  /** Applies an action to the cart reducer and saves the result to localStorage. */
  send: (action: CartAction) => void
  /** The reducer state of the last render. Components read this to render, so they re-render on a change. */
  state: CartState
}

export const CartContext = createContext<CartContextValue | null>(null)

/** The cart context. Throws when `CartProvider` is missing, which is a setup bug. */
export function useCartContext(): CartContextValue {
  const value = use(CartContext)
  if (!value) throw new Error('useCart needs <CartProvider> above it.')
  return value
}
