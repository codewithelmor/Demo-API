import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useCountdown } from '@/hooks/use-countdown'

describe('useCountdown', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })
  afterEach(() => {
    vi.useRealTimers()
  })

  it('is 0 without a source', () => {
    const { result } = renderHook(() => useCountdown(undefined, 30))
    expect(result.current).toBe(0)
  })

  it('counts down once per second and stops at 0', () => {
    const source = {}
    const { result } = renderHook(() => useCountdown(source, 3))
    expect(result.current).toBe(3)

    act(() => {
      vi.advanceTimersByTime(1000)
    })
    expect(result.current).toBe(2)
    act(() => {
      vi.advanceTimersByTime(2000)
    })
    expect(result.current).toBe(0)
    act(() => {
      vi.advanceTimersByTime(5000)
    })
    expect(result.current).toBe(0)
  })

  it('restarts when a new source (a new 429) arrives', () => {
    let source: object = {}
    const { result, rerender } = renderHook(() => useCountdown(source, 5))
    act(() => {
      vi.advanceTimersByTime(4000)
    })
    expect(result.current).toBe(1)

    source = {}
    rerender()
    expect(result.current).toBe(5)
    act(() => {
      vi.advanceTimersByTime(1000)
    })
    expect(result.current).toBe(4)
  })

  it('goes back to 0 when the source is cleared', () => {
    let source: object | undefined = {}
    const { result, rerender } = renderHook(() => useCountdown(source, 5))
    expect(result.current).toBe(5)

    source = undefined
    rerender()
    expect(result.current).toBe(0)
  })
})
