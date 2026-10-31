import { useEffect, useState } from 'react'

/**
 * React hook that counts down from `seconds` to 0, once per second, whenever `source` changes to a new
 * object (for example a new 429 "too many requests" error). Pass `undefined` as source for "not counting".
 * Used to keep a submit button disabled after a 429 (NFR-F23).
 *
 * @param source  Any object that identifies the event to count down for.
 * @param seconds Where to start counting.
 * @returns The seconds left (0 when finished or not counting).
 */
export function useCountdown(source: object | undefined, seconds: number): number {
  const [state, setState] = useState<{ source: object | undefined; left: number }>({
    source,
    left: source === undefined ? 0 : seconds,
  })

  // A new source restarts the countdown (state adjusted during render, as React recommends).
  if (state.source !== source) {
    setState({ source, left: source === undefined ? 0 : seconds })
  }

  useEffect(() => {
    if (source === undefined) return
    const timer = setInterval(() => {
      setState((current) => ({ ...current, left: Math.max(0, current.left - 1) }))
    }, 1000)
    return () => {
      clearInterval(timer)
    }
  }, [source])

  return state.left
}
