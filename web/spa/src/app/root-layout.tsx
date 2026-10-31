/** The frame around every page: skip link, header with navigation, and the main content area. */
import { NavLink, Outlet, useNavigation } from 'react-router'
import { CartLink } from '@/components/cart-link'
import { useSession } from '@/lib/session'
import { SignOutButton } from '@/features/auth/sign-out-button'
import { useFocusHeadingOnNavigate } from '@/hooks/use-focus-heading-on-navigate'
import { cn } from '@/lib/utils'

/** CSS classes for a navigation link. React Router tells us whether the link points at the current page. */
const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  cn(
    'rounded-md px-3 py-2 text-sm font-medium hover:bg-muted focus-visible:outline-2 focus-visible:outline-ring',
    isActive && 'bg-muted',
  )

/**
 * The app shell, rendered around every page (the page itself appears where `<Outlet />` is).
 *
 * - The skip link lets keyboard and screen-reader users jump over the header straight to `<main>`.
 * - The navigation, cart link and sign-out button only show when someone is signed in, and the links
 *   depend on the user's role (Customer or Admin).
 * - `aria-busy` tells assistive technology that the page content is loading during a navigation.
 */
export function RootLayout() {
  useFocusHeadingOnNavigate()
  const navigation = useNavigation()
  const user = useSession()

  return (
    <div className="min-h-dvh">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:top-4 focus:left-4 focus:rounded-md focus:bg-background focus:p-2"
      >
        Skip to main content
      </a>
      <header className="border-b">
        <div className="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-2 p-4">
          <span className="font-semibold">Orders Shop</span>
          {user && (
            <>
              <nav aria-label="Main">
                <ul className="flex gap-1">
                  <li>
                    <NavLink to="/products" className={navLinkClass}>
                      Products
                    </NavLink>
                  </li>
                  {user.roles.includes('Customer') && (
                    <li>
                      <NavLink to="/orders" className={navLinkClass}>
                        My orders
                      </NavLink>
                    </li>
                  )}
                  {user.roles.includes('Admin') && (
                    <li>
                      <NavLink to="/admin" className={navLinkClass}>
                        Back office
                      </NavLink>
                    </li>
                  )}
                </ul>
              </nav>
              <div className="flex items-center gap-3">
                <CartLink />
                {user.roles.includes('Customer') && (
                  <NavLink to="/account" className={navLinkClass}>
                    My account
                  </NavLink>
                )}
                <span className="text-sm text-muted-foreground">{user.email}</span>
                <SignOutButton />
              </div>
            </>
          )}
        </div>
      </header>
      <main id="main" className="mx-auto max-w-5xl p-4" aria-busy={navigation.state === 'loading'}>
        <Outlet />
      </main>
    </div>
  )
}
