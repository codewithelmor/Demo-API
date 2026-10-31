// shadcn/ui component, copied into the project (you own and can change this code).
import { Label as LabelPrimitive } from 'radix-ui'
import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

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
