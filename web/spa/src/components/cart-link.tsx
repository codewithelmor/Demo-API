import { ShoppingCart } from 'lucide-react'
import { NavLink } from 'react-router'
import { useCart } from '@/hooks/use-cart'
import { cn } from '@/lib/utils'

// The header cart link with a count of distinct products. Only customers have a cart.
export function CartLink() {
  const { count, enabled } = useCart()
  if (!enabled) return null
  return (
    <NavLink
      to="/cart"
      aria-label={`Cart, ${String(count)} ${count === 1 ? 'product' : 'products'}`}
      className={({ isActive }) =>
        cn(
          'inline-flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium hover:bg-muted focus-visible:outline-2 focus-visible:outline-ring',
          isActive && 'bg-muted',
        )
      }
    >
      <ShoppingCart aria-hidden="true" size={16} />
      <span aria-hidden="true">Cart</span>
      <span
        aria-hidden="true"
        className="min-w-5 rounded-full bg-primary px-1.5 text-center text-xs text-primary-foreground"
      >
        {count}
      </span>
    </NavLink>
  )
}
