// shadcn/ui "native select", copied into the project (you own and can change this code).
import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

function NativeSelect({ className, ...props }: ComponentProps<'select'>) {
  return (
    <select
      data-slot="native-select"
      className={cn(
        'h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring disabled:opacity-50',
        className,
      )}
      {...props}
    />
  )
}

export { NativeSelect }
