// shadcn/ui component, copied into the project (you own and can change this code).
// Styling uses Tailwind CSS classes; `cn()` merges them so a caller's `className` can override the defaults.
import { cva, type VariantProps } from 'class-variance-authority'
import { Slot } from 'radix-ui'
import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

/**
 * Class names for each button look. Also used on links (`<Link className={buttonVariants()}>`) so a link
 * can look like a button.
 */
const buttonVariants = cva(
  'inline-flex items-center justify-center gap-2 rounded-md text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring disabled:pointer-events-none disabled:opacity-50',
  {
    variants: {
      variant: {
        default: 'bg-primary text-primary-foreground hover:bg-primary/90',
        outline: 'border border-input bg-background hover:bg-muted aria-pressed:bg-muted',
        ghost: 'hover:bg-muted',
        destructive:
          'border border-destructive bg-background text-destructive hover:bg-destructive hover:text-white',
      },
      size: {
        default: 'h-10 px-4 py-2',
        sm: 'h-9 px-3',
        icon: 'size-10',
      },
    },
    defaultVariants: { variant: 'default', size: 'default' },
  },
)

/** All normal `<button>` props, plus `variant`, `size` and `asChild`. */
type ButtonProps = ComponentProps<'button'> &
  VariantProps<typeof buttonVariants> & {
    /** When true, the button's styles and behaviour are applied to its single child element instead of a `<button>`. */
    asChild?: boolean
  }

/**
 * The standard button. Variants: `default` (main action), `outline`, `ghost`, `destructive` (dangerous action).
 * Sizes: `default`, `sm`, `icon` (square, for icon-only buttons; give them an `aria-label`).
 */
function Button({ className, variant, size, asChild = false, ...props }: ButtonProps) {
  const Comp = asChild ? Slot.Root : 'button'
  return (
    <Comp
      data-slot="button"
      className={cn(buttonVariants({ variant, size }), className)}
      {...props}
    />
  )
}

export { Button, buttonVariants }
