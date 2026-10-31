import { Link, type LinkProps } from 'react-router'
import { cn } from '@/lib/utils'

// An in-app link in running text or a form footer. Underlined, so it is not only colour (WCAG 1.4.1).
export function TextLink({ className, ...props }: LinkProps) {
  return <Link className={cn('text-sm underline underline-offset-4', className)} {...props} />
}
