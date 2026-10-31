import { useEffect, useRef } from 'react'
import { useLocation } from 'react-router'

// After a client-side navigation, move focus to the new page's <h1> so screen reader and
// keyboard users start at the new content. The first page load keeps the browser default.
export function useFocusHeadingOnNavigate() {
  const { pathname } = useLocation()
  const previous = useRef(pathname)

  useEffect(() => {
    if (previous.current === pathname) return
    previous.current = pathname
    document.querySelector<HTMLElement>('main h1')?.focus()
  }, [pathname])
}
