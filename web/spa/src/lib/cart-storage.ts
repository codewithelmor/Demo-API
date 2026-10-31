import { z } from '@/lib/zod'
import { sha256Hex } from '@/lib/sha256'

// The cart lives in localStorage and is the only data the browser keeps (NFR-F14). A line holds the
// product ID, the quantity and the unit price the customer saw, nothing else (FR-F028, UX-D1).
// The key is cart:{first 16 hex of SHA-256(identity)} (NFR-F41), so no ID or email is readable in storage.
// The identity is the customer ID: the BFF's /bff/user does not send the user ID.

export const MAX_LINES = 50
export const MAX_QUANTITY = 1000

const lineSchema = z.object({
  productId: z.uuid(),
  quantity: z.int().min(1).max(MAX_QUANTITY),
  price: z.number().min(0),
})

// Anything that does not match (edited by hand, an older format) is dropped, never an error. A bad line is
// skipped on its own, so one broken entry does not empty the whole cart; lines beyond the maximum and
// a repeated product are dropped too.
const storedSchema = z.object({ lines: z.array(z.unknown()) })

export type CartLine = z.infer<typeof lineSchema>

const EMPTY: readonly CartLine[] = []

export function cartKeyFor(customerId: string): string {
  return `cart:${sha256Hex(customerId).slice(0, 16)}`
}

function readRaw(key: string): string | null {
  try {
    return window.localStorage.getItem(key)
  } catch {
    return null // storage blocked: behave as an empty cart
  }
}

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

// useSyncExternalStore needs the same object back while nothing changed.
const cache = new Map<string, { raw: string | null; lines: readonly CartLine[] }>()

export function readCart(key: string): readonly CartLine[] {
  const raw = readRaw(key)
  const cached = cache.get(key)
  if (cached?.raw === raw) return cached.lines
  const lines = parse(raw)
  cache.set(key, { raw, lines })
  return lines
}

const listeners = new Set<() => void>()

function notify() {
  listeners.forEach((listener) => {
    listener()
  })
}

export function subscribeCart(listener: () => void): () => void {
  listeners.add(listener)
  // Changes made in another tab.
  window.addEventListener('storage', listener)
  return () => {
    listeners.delete(listener)
    window.removeEventListener('storage', listener)
  }
}

export function writeCart(key: string, lines: readonly CartLine[]): void {
  try {
    if (lines.length === 0) window.localStorage.removeItem(key)
    else window.localStorage.setItem(key, JSON.stringify({ lines }))
  } catch {
    // Storage full or blocked: the cart simply does not persist.
  }
  notify()
}

// Sign out and Delete account call this (NFR-F41).
export function clearCart(key: string): void {
  writeCart(key, EMPTY)
}

export function clearCartFor(customerId: string | null | undefined): void {
  if (customerId) clearCart(cartKeyFor(customerId))
}
