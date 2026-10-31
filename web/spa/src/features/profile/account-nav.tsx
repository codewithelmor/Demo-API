import type { ReactNode } from 'react'
import { Link, useLocation } from 'react-router'
import { cn } from '@/lib/utils'

const links = [
  { to: '/account', label: 'Profile', paths: ['/account', '/account/edit'] },
  { to: '/account/password', label: 'Change password', paths: ['/account/password'] },
  { to: '/account/delete', label: 'Delete account', paths: ['/account/delete'] },
]

// The list of account pages (information architecture, My account). "Profile" stays current on the edit page too.
export function AccountNav() {
  const { pathname } = useLocation()
  return (
    <nav aria-label="Account" className="md:w-48">
      <ul className="flex flex-wrap gap-1 md:grid">
        {links.map((link) => {
          const current = link.paths.includes(pathname)
          return (
            <li key={link.to}>
              <Link
                to={link.to}
                aria-current={current ? 'page' : undefined}
                className={cn(
                  'block rounded-md px-3 py-2 text-sm font-medium hover:bg-muted focus-visible:outline-2 focus-visible:outline-ring',
                  current && 'bg-muted',
                )}
              >
                {link.label}
              </Link>
            </li>
          )
        })}
      </ul>
    </nav>
  )
}

// The page frame: the account links next to the page.
export function AccountLayout({ children }: { children: ReactNode }) {
  return (
    <div className="grid gap-6 md:grid-cols-[12rem_1fr]">
      <AccountNav />
      <div className="grid max-w-xl content-start gap-4">{children}</div>
    </div>
  )
}
