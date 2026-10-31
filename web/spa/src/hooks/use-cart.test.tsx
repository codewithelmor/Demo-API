import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, renderHook } from '@testing-library/react'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it } from 'vitest'
import { useCart } from '@/hooks/use-cart'
import { sessionKey } from '@/lib/session'
import { customerUser, productId } from '@/test/data'

function setup() {
  const client = new QueryClient()
  client.setQueryData(sessionKey, customerUser)
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )
  return renderHook(() => useCart(), { wrapper })
}

const product = (n: number) => ({ id: productId(n), price: 10, stockQuantity: 100 })

beforeEach(() => {
  window.localStorage.clear()
})

describe('useCart actions', () => {
  it('two quick adds in one event keep both lines (no stale snapshot)', () => {
    const { result } = setup()

    act(() => {
      result.current.add(product(1))
      result.current.add(product(2))
    })

    expect(result.current.lines.map((line) => line.productId)).toEqual([productId(1), productId(2)])
  })

  it('an add and a remove in one event both apply', () => {
    const { result } = setup()
    act(() => {
      result.current.add(product(1))
    })

    act(() => {
      result.current.remove(productId(1))
      result.current.add(product(2))
    })

    expect(result.current.lines.map((line) => line.productId)).toEqual([productId(2)])
  })
})
