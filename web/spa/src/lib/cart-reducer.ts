import { MAX_LINES, MAX_QUANTITY, readCart, type CartLine } from '@/lib/cart-storage'

/** Outcome of trying to add a product to the cart. */
export type AddResult = 'added' | 'cart-full' | 'max-quantity' | 'stock-limit' | 'no-cart'

/** The facts about a product the cart needs when adding it. */
export interface CartProduct {
  id: string
  price: number
  stockQuantity: number
}

/** The cart lines per storage key (one key per customer). A cart that is not loaded yet is missing here. */
export interface CartState {
  byKey: Readonly<Record<string, readonly CartLine[]>>
}

export const initialCartState: CartState = { byKey: {} }

/** Everything that can change a cart. Each action names the cart (`key`) it applies to. */
export type CartAction =
  | { type: 'add'; key: string; product: CartProduct; quantity: number }
  | { type: 'setQuantity'; key: string; productId: string; quantity: number }
  | { type: 'remove'; key: string; productId: string }
  | { type: 'restore'; key: string; line: CartLine }
  | { type: 'clear'; key: string }
  /** The stored cart changed outside this tab's reducer (another tab, sign out): take the stored lines. */
  | { type: 'sync'; key: string; lines: readonly CartLine[] }

/** The lines of a cart for showing it. A cart that was not loaded yet is read from storage (the same array while storage is unchanged). */
export function linesOf(state: CartState, key: string): readonly CartLine[] {
  return state.byKey[key] ?? readCart(key)
}

/** True when both lists hold the same lines in the same order. */
export function sameLines(a: readonly CartLine[], b: readonly CartLine[]): boolean {
  return (
    a === b ||
    (a.length === b.length &&
      a.every(
        (l, i) =>
          l.productId === b[i]?.productId && l.quantity === b[i].quantity && l.price === b[i].price,
      ))
  )
}

/** Adds `quantity` of a product, respecting the line, quantity and stock limits. Says what happened and what the lines are now. */
export function addLines(
  lines: readonly CartLine[],
  product: CartProduct,
  quantity: number,
): { result: AddResult; lines: readonly CartLine[] } {
  const existing = lines.find((line) => line.productId === product.id)
  if (!existing && lines.length >= MAX_LINES) return { result: 'cart-full', lines }
  const total = (existing?.quantity ?? 0) + quantity
  if (total > MAX_QUANTITY) return { result: 'max-quantity', lines }
  if (total > product.stockQuantity) return { result: 'stock-limit', lines }
  // The price seen is the one on the screen when the customer added it.
  const line: CartLine = { productId: product.id, quantity: total, price: product.price }
  return {
    result: 'added',
    lines: existing ? lines.map((l) => (l.productId === product.id ? line : l)) : [...lines, line],
  }
}

/** The lines after an action. Pure: the same lines and action always give the same answer. */
function nextLines(lines: readonly CartLine[], action: CartAction): readonly CartLine[] {
  switch (action.type) {
    case 'add':
      return addLines(lines, action.product, action.quantity).lines
    case 'setQuantity':
      return lines.map((line) =>
        line.productId === action.productId ? { ...line, quantity: action.quantity } : line,
      )
    case 'remove':
      return lines.filter((line) => line.productId !== action.productId)
    case 'restore':
      // Undo puts a removed line back, unless the cart is full or already has the product.
      return lines.length < MAX_LINES && !lines.some((l) => l.productId === action.line.productId)
        ? [...lines, action.line]
        : lines
    case 'clear':
      return []
    case 'sync':
      return action.lines
  }
}

/** The cart reducer for `useReducer`. Returns the same state object when an action changes nothing. */
export function cartReducer(state: CartState, action: CartAction): CartState {
  // No storage read here: the provider loads a cart into the state (a 'sync' action) before changing it.
  const current = state.byKey[action.key] ?? []
  const next = nextLines(current, action)
  if (sameLines(current, next) && action.key in state.byKey) return state
  return { byKey: { ...state.byKey, [action.key]: next } }
}
