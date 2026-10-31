// shadcn/ui component, copied into the project (you own and can change this code).
// Styling uses Tailwind CSS classes; `cn()` merges them so a caller's `className` can override the defaults.
import { cva, type VariantProps } from 'class-variance-authority'
import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

/**
 * Class names for each badge look. `cva` (class-variance-authority) builds the class string from the
 * chosen `variant`.
 */
const badgeVariants = cva(
  'inline-flex items-center rounded-md border px-2 py-0.5 text-xs font-medium whitespace-nowrap',
  {
    variants: {
      variant: {
        default: 'border-transparent bg-primary text-primary-foreground',
        outline: 'text-foreground',
        muted: 'border-transparent bg-muted text-foreground',
      },
    },
    defaultVariants: { variant: 'outline' },
  },
)

/** A small label for a status or a count. Variants: `default` (filled), `outline` (default), `muted` (grey). */
function Badge({
  className,
  variant,
  ...props
}: ComponentProps<'span'> & VariantProps<typeof badgeVariants>) {
  return <span data-slot="badge" className={cn(badgeVariants({ variant }), className)} {...props} />
}

export { Badge }
