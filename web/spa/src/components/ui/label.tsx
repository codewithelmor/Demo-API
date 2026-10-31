// shadcn/ui component, copied into the project (you own and can change this code).
// Styling uses Tailwind CSS classes; `cn()` merges them so a caller's `className` can override the defaults.
import { Label as LabelPrimitive } from 'radix-ui'
import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

/** The text label for a form control. Connect it with `htmlFor` = the control's `id`. */
function Label({ className, ...props }: ComponentProps<typeof LabelPrimitive.Root>) {
  return (
    <LabelPrimitive.Root
      data-slot="label"
      className={cn('text-sm leading-none font-medium', className)}
      {...props}
    />
  )
}

export { Label }
