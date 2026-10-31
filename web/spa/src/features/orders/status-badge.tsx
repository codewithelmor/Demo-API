import type { OrderStatus } from '@/api/orders'
import { Badge } from '@/components/ui/badge'

// The status is always written out, never shown by colour alone.
export function StatusBadge({ status }: { status: OrderStatus }) {
  const variant = status === 'Cancelled' || status === 'Completed' ? 'muted' : 'default'
  return <Badge variant={variant}>{status}</Badge>
}

export const shortId = (id: string) => id.slice(0, 8).toUpperCase()
