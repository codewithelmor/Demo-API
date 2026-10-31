import { z } from '@/lib/zod'
import { sha256Hex } from '@/lib/sha256'

/**
 * Saves the shopping cart in the browser, so it survives a page reload.
 *
 * The cart lives in `localStorage` (a small key/value store the browser keeps per site) and is the only
 * data the browser keeps (NFR-F14). A line holds the product ID, the quantity and the unit price the
 * customer saw, nothing else (FR-F028, UX-D1).
 * The key is `cart:{first 16 hex of SHA-256(identity)}` (NFR-F41), so no ID or email is readable in storage.
 * The identity is the customer ID: the BFF's /bff/user does not send the user ID.
 *
 * Components should use the `useCart` hook instead of calling these functions directly.
 */

/** Most different products one order may hold. */
export const MAX_LINES = 50
/** Most units of one product in one order. */
export const MAX_QUANTITY = 1000

/** Shape of one stored cart line. Zod checks stored data because the user can edit localStorage by hand. */
const lineSchema = z.object({
  productId: z.uuid(),
  quantity: z.int().min(1).max(MAX_QUANTITY),
  price: z.number().min(0),
})

/**
 * Shape of the whole stored cart. Anything that does not match (edited by hand, an older format) is
 * dropped, never an error. A bad line is skipped on its own, so one broken entry does not empty the
 * whole cart; lines beyond the maximum and a repeated product are dropped too.
 */
const storedSchema = z.object({ lines: z.array(z.unknown()) })

/** One cart line: product ID, quantity, and the unit price the customer saw when adding it. */
export type CartLine = z.infer<typeof lineSchema>

const EMPTY: readonly CartLine[] = []

/** The localStorage key for a customer's cart. Hashed so the customer ID is not readable in storage. */
export function cartKeyFor(customerId: string): string {
  return `cart:${sha256Hex(customerId).slice(0, 16)}`
}

/** Reads the stored text for a key, or `null` when there is none or the browser blocks storage. */
function readRaw(key: string): string | null {
  try {
    return window.localStorage.getItem(key)
  } catch {
    return null // storage blocked: behave as an empty cart
  }
}

/** Turns stored text into valid cart lines, quietly skipping anything invalid. */
function parse(raw: string | null): readonly CartLine[] {
  if (raw === null) return EMPTY
  try {
    const stored = storedSchema.safeParse(JSON.parse(raw))
    if (!stored.success) return EMPTY
    const lines: CartLine[] = []
    const seen = new Set<string>()
    for (const candidate of stored.data.lines) {
      const line = lineSchema.safeParse(candidate)
      if (!line.success || seen.has(line.data.productId)) continue
      seen.add(line.data.productId)
      lines.push(line.data)
      if (lines.length === MAX_LINES) break
    }
    return lines
  } catch {
    return EMPTY
  }
}

// React's useSyncExternalStore needs the very same array back while nothing changed, or it re-renders forever.
// So the parsed result is remembered per key and reused while the stored text is unchanged.
const cache = new Map<string, { raw: string | null; lines: readonly CartLine[] }>()

/** Returns the cart lines stored under `key` (empty when none). Safe to call during rendering. */
export function readCart(key: string): readonly CartLine[] {
  const raw = readRaw(key)
  const cached = cache.get(key)
  if (cached?.raw === raw) return cached.lines
  const lines = parse(raw)
  cache.set(key, { raw, lines })
  return lines
}

/** Functions to call when the cart changes in this tab. */
const listeners = new Set<() => void>()

/** Tells every subscriber that the cart changed. */
function notify() {
  listeners.forEach((listener) => {
    listener()
  })
}

/**
 * Registers a listener that runs when the cart changes, in this tab or another one.
 * This is the `subscribe` function React's `useSyncExternalStore` expects.
 *
 * @returns A function that removes the listener again.
 */
export function subscribeCart(listener: () => void): () => void {
  listeners.add(listener)
  // Changes made in another tab.
  window.addEventListener('storage', listener)
  return () => {
    listeners.delete(listener)
    window.removeEventListener('storage', listener)
  }
}

/** Saves the cart lines (an empty list removes the entry) and notifies subscribers. */
export function writeCart(key: string, lines: readonly CartLine[]): void {
  try {
    if (lines.length === 0) window.localStorage.removeItem(key)
    else window.localStorage.setItem(key, JSON.stringify({ lines }))
  } catch {
    // Storage full or blocked: the cart simply does not persist.
  }
  notify()
}

/** Empties the cart stored under `key`. Sign out and Delete account call this (NFR-F41). */
export function clearCart(key: string): void {
  writeCart(key, EMPTY)
}

/** Empties the cart of a customer ID; does nothing when there is no customer (for example an Admin). */
export function clearCartFor(customerId: string | null | undefined): void {
  if (customerId) clearCart(cartKeyFor(customerId))
}
