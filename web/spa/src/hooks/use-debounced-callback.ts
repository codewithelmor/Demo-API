import { useEffect, useRef } from 'react'

/**
 * React hook that delays a function until the caller has stopped calling it for `delay` ms ("debounce").
 * Use it for things like search-as-you-type, so a request is not sent per keystroke.
 *
 * Returns:
 * - `run(...args)`: restarts the wait; after it, the latest callback runs with these arguments.
 * - `cancel()`: drops the pending call (for example when the input is cleared or too short).
 *
 * A pending call is also dropped when the component unmounts, so nothing fires on a screen that is gone.
 */
export function useDebouncedCallback<Args extends unknown[]>(
  callback: (...args: Args) => void,
  delay: number,
) {
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)
  // Always the newest callback, so a call that fires later never uses stale props.
  const latest = useRef(callback)

  useEffect(() => {
    latest.current = callback
  })

  useEffect(
    () => () => {
      clearTimeout(timer.current)
    },
    [],
  )

  function cancel() {
    clearTimeout(timer.current)
  }

  function run(...args: Args) {
    clearTimeout(timer.current)
    timer.current = setTimeout(() => {
      latest.current(...args)
    }, delay)
  }

  return { run, cancel }
}
