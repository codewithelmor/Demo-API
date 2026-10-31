/** The "page not found" page (HTTP 404 equivalent). */
import { Link } from 'react-router'
import { buttonVariants } from '@/components/ui/button'

/**
 * SY-1: shown when the URL matches no route, or when the API says a resource does not exist.
 * The heading has `tabIndex={-1}` so code can move keyboard/screen-reader focus to it.
 */
export function NotFoundPage() {
  return (
    <section className="grid max-w-prose gap-4">
      <title>Page not found · Orders Shop</title>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        Page not found
      </h1>
      <p>Check the address, or go back to the start.</p>
      <div>
        <Link to="/" className={buttonVariants()}>
          Go to the start
        </Link>
      </div>
    </section>
  )
}
