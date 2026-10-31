import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useDebouncedCallback } from '@/hooks/use-debounced-callback'

beforeEach(() => {
  vi.useFakeTimers()
})
afterEach(() => {
  vi.useRealTimers()
})

describe('useDebouncedCallback', () => {
  it('calls once, with the last arguments, after the quiet period', () => {
    const callback = vi.fn()
    const { result } = renderHook(() => useDebouncedCallback(callback, 300))

    act(() => {
      result.current.run('a')
      vi.advanceTimersByTime(200)
      result.current.run('ab')
      vi.advanceTimersByTime(299)
    })
    expect(callback).not.toHaveBeenCalled()

    act(() => {
      vi.advanceTimersByTime(1)
    })
    expect(callback).toHaveBeenCalledExactlyOnceWith('ab')
  })

  it('cancel and unmount drop the pending call', () => {
    const callback = vi.fn()
    const { result, unmount } = renderHook(() => useDebouncedCallback(callback, 300))

    act(() => {
      result.current.run('a')
      result.current.cancel()
      vi.advanceTimersByTime(300)
    })
    act(() => {
      result.current.run('b')
    })
    unmount()
    act(() => {
      vi.advanceTimersByTime(300)
    })

    expect(callback).not.toHaveBeenCalled()
  })
})
