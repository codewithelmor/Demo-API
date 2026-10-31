import { beforeEach, describe, expect, it } from 'vitest'
import { cartKeyFor, clearCartFor, readCart, writeCart } from '@/lib/cart-storage'
import { sha256Hex } from '@/lib/sha256'

const customerId = '5b1b7c9e-0000-4000-8000-000000000001'
const key = cartKeyFor(customerId)
const line = (n: number, quantity = 1, price = 10) => ({
  productId: `00000000-0000-4000-8000-${String(n).padStart(12, '0')}`,
  quantity,
  price,
})

beforeEach(() => {
  window.localStorage.clear()
})

describe('cart storage (NFR-F41)', () => {
  it('names the key cart: plus the first 16 hex of SHA-256 of the identity', () => {
    expect(key).toBe(`cart:${sha256Hex(customerId).slice(0, 16)}`)
    expect(key).toMatch(/^cart:[0-9a-f]{16}$/)
    expect(key).not.toContain(customerId)
    expect(cartKeyFor('other-customer')).not.toBe(key)
  })

  it('round-trips lines and stores only ids, quantities and prices', () => {
    writeCart(key, [line(1, 2, 11.5)])

    expect(readCart(key)).toEqual([line(1, 2, 11.5)])
    expect(JSON.parse(window.localStorage.getItem(key) ?? '')).toEqual({
      lines: [line(1, 2, 11.5)],
    })
    // Nothing else is stored for the cart.
    expect(window.localStorage.length).toBe(1)
  })

  it('treats a stored value that is not a cart as an empty cart', () => {
    for (const raw of ['not json', '{"lines":"x"}', '[]', 'null', '{}']) {
      window.localStorage.setItem(key, raw)
      expect(readCart(key), raw).toEqual([])
    }
  })

  it('drops a bad line on its own and keeps the good ones', () => {
    const bad: unknown[] = [
      line(2, 0), // quantity below 1
      line(3, 1001), // quantity above 1000
      line(4, 1.5), // not a whole number
      { ...line(5), productId: 'not-a-guid' },
      { ...line(6), price: -1 },
      'text',
      null,
    ]
    window.localStorage.setItem(key, JSON.stringify({ lines: [line(1), ...bad, line(7, 3)] }))

    expect(readCart(key)).toEqual([line(1), line(7, 3)])
  })

  it('keeps the first of a repeated product and the first 50 lines', () => {
    window.localStorage.setItem(key, JSON.stringify({ lines: [line(1, 2), line(1, 5)] }))
    expect(readCart(key)).toEqual([line(1, 2)])

    const many = Array.from({ length: 60 }, (_, i) => line(i + 1))
    window.localStorage.setItem(key, JSON.stringify({ lines: many }))
    expect(readCart(key)).toHaveLength(50)
  })

  it('accepts exactly 50 lines', () => {
    const lines = Array.from({ length: 50 }, (_, i) => line(i + 1))
    writeCart(key, lines)
    expect(readCart(key)).toHaveLength(50)
  })

  it('returns the same array while nothing changed (for useSyncExternalStore)', () => {
    writeCart(key, [line(1)])
    expect(readCart(key)).toBe(readCart(key))
  })

  it('removes the key when the cart is cleared, and only that key', () => {
    window.localStorage.setItem('other', 'x')
    writeCart(key, [line(1)])

    clearCartFor(customerId)

    expect(window.localStorage.getItem(key)).toBeNull()
    expect(window.localStorage.getItem('other')).toBe('x')
    expect(readCart(key)).toEqual([])
  })

  it('does nothing for a user without a customer ID', () => {
    writeCart(key, [line(1)])
    clearCartFor(null)
    expect(readCart(key)).toHaveLength(1)
  })
})
