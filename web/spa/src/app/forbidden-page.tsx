import { Link } from 'react-router'
import { buttonVariants } from '@/components/ui/button'

// SY-2: shown in place on guarded routes. No API call is made.
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
