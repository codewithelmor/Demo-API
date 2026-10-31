import { beforeEach, describe, expect, it } from 'vitest'
import { cartReducer, initialCartState, linesOf } from '@/lib/cart-reducer'
import { cartKeyFor } from '@/lib/cart-storage'
import { productId } from '@/test/data'

const key = cartKeyFor('7c9e6679-7425-40de-944b-e07fc1f90ae7')
const product = { id: productId(1), price: 10, stockQuantity: 5 }

beforeEach(() => {
  window.localStorage.clear()
})

describe('cartReducer', () => {
  it('adds, then refuses more than the stock', () => {
    const one = cartReducer(initialCartState, { type: 'add', key, product, quantity: 5 })
    const again = cartReducer(one, { type: 'add', key, product, quantity: 1 })

    expect(linesOf(one, key)[0]?.quantity).toBe(5)
    expect(again).toBe(one)
  })

  it('removes and restores a line, and clears', () => {
    const added = cartReducer(initialCartState, { type: 'add', key, product, quantity: 2 })
    const [line] = linesOf(added, key)
    if (!line) throw new Error('no line')
    const removed = cartReducer(added, { type: 'remove', key, productId: product.id })
    const restored = cartReducer(removed, { type: 'restore', key, line })

    expect(linesOf(removed, key)).toHaveLength(0)
    expect(linesOf(restored, key)).toEqual([line])
    expect(linesOf(cartReducer(restored, { type: 'clear', key }), key)).toHaveLength(0)
  })

  it('sync takes the stored lines', () => {
    const lines = [{ productId: productId(2), quantity: 3, price: 4 }]

    expect(linesOf(cartReducer(initialCartState, { type: 'sync', key, lines }), key)).toEqual(lines)
  })
})
