import { useCallback, useEffect, useReducer, useRef, type ReactNode } from 'react'
import { CartContext } from '@/hooks/cart-context'
import {
  cartReducer,
  initialCartState,
  linesOf,
  sameLines,
  type CartAction,
} from '@/lib/cart-reducer'
import { readCart, subscribeCart, writeCart } from '@/lib/cart-storage'

/**
 * Holds the cart state with `useReducer` and shares it with the whole app.
 *
 * The reducer itself stays pure. This component adds the two side effects around it: saving to
 * localStorage after each change, and loading the stored cart again when it changes outside the
 * reducer (another tab, sign out, delete account).
 */
export function CartProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(cartReducer, initialCartState)
  // The newest state, also between two actions in one event (the rendered `state` is one render behind).
  const latest = useRef(state)

  const send = useCallback((action: CartAction) => {
    // Load the cart from storage first, before this change is saved to it.
    if (!(action.key in latest.current.byKey)) {
      const load: CartAction = { type: 'sync', key: action.key, lines: readCart(action.key) }
      latest.current = cartReducer(latest.current, load)
      dispatch(load)
    }
    const before = linesOf(latest.current, action.key)
    latest.current = cartReducer(latest.current, action)
    dispatch(action)
    const after = linesOf(latest.current, action.key)
    // 'sync' comes from storage, so there is nothing to save.
    if (action.type !== 'sync' && !sameLines(before, after)) writeCart(action.key, after)
  }, [])

  const getLines = useCallback((key: string) => linesOf(latest.current, key), [])

  useEffect(
    () =>
      subscribeCart(() => {
        for (const [key, lines] of Object.entries(latest.current.byKey)) {
          const stored = readCart(key)
          if (!sameLines(stored, lines)) send({ type: 'sync', key, lines: stored })
        }
      }),
    [send],
  )

  return <CartContext value={{ getLines, send, state }}>{children}</CartContext>
}
