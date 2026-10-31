import type { OrderStatus } from '@/api/orders'
import { Badge } from '@/components/ui/badge'

/**
 * The status is always written out, never shown by colour alone.
 * Shows an order status as a small badge.
 */
export function StatusBadge({ status }: { status: OrderStatus }) {
  const variant = status === 'Cancelled' || status === 'Completed' ? 'muted' : 'default'
  return <Badge variant={variant}>{status}</Badge>
}

/** The first 8 characters of an ID, upper case: a readable order number for people. */
export const shortId = (id: string) => id.slice(0, 8).toUpperCase()
