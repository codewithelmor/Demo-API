/** The "you do not have access" page (HTTP 403 equivalent). */
import { Link } from 'react-router'
import { buttonVariants } from '@/components/ui/button'

/**
 * SY-2: shown in place on guarded routes when a signed-in user has the wrong role.
 * "In place" means the URL does not change. No API call is made.
 *
 * `<title>` inside a component sets the browser tab title (React 19 moves it into `<head>`).
 * `tabIndex={-1}` lets code focus the heading (see `useFocusHeadingOnNavigate`) so screen readers announce the new page.
 */
export function ForbiddenPage() {
  return (
    <section className="grid max-w-prose gap-4">
      <title>Not allowed · Orders Shop</title>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        You do not have access to this page.
      </h1>
      <p>This area is for administrators.</p>
      <div>
        <Link to="/products" className={buttonVariants()}>
          Go to Products
        </Link>
      </div>
    </section>
  )
}
