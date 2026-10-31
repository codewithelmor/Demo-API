import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

/**
 * Joins CSS class names, skipping falsy values, and lets later Tailwind classes win over earlier ones
 * (shadcn/ui convention). Example: `cn('px-2', isActive && 'bg-muted', 'px-4')` gives `'bg-muted px-4'`.
 */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}
