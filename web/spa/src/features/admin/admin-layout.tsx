/** The frame of the back office: a section navigation beside the admin page (FR-F050). */
import { NavLink, Outlet } from 'react-router'
import { cn } from '@/lib/utils'

/** CSS classes for a section link. */
const linkClass = ({ isActive }: { isActive: boolean }) =>
  cn(
    'rounded-md px-3 py-2 text-sm font-medium hover:bg-muted focus-visible:outline-2 focus-visible:outline-ring',
    isActive && 'bg-muted',
  )

/**
 * Wraps every `/admin` page. Only sections that exist are linked; Customers, Orders, Reports, Users and
 * Audit logs join the list as their slices are built.
 */
export function AdminLayout() {
  return (
    <div className="grid gap-4 md:grid-cols-[12rem_1fr]">
      <nav aria-label="Back office">
        <ul className="flex gap-1 md:grid">
          <li>
            <NavLink to="/admin" end className={linkClass}>
              Dashboard
            </NavLink>
          </li>
          <li>
            <NavLink to="/admin/products" className={linkClass}>
              Products
            </NavLink>
          </li>
        </ul>
      </nav>
      <div className="min-w-0">
        <Outlet />
      </div>
    </div>
  )
}
