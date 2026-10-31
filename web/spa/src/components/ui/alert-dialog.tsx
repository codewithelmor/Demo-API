// shadcn/ui component, copied into the project (you own and can change this code).
// Styling uses Tailwind CSS classes; `cn()` merges them so a caller's `className` can override the defaults.
// Built on Radix AlertDialog (from the radix-ui package already installed): focus is trapped,
// Escape closes it, and focus returns to the button that opened it.
import { AlertDialog as AlertDialogPrimitive } from 'radix-ui'
import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

// An alert dialog interrupts the user for a decision (for example "Cancel this order?"). Parts:
// Root holds the open/closed state, Trigger is the button that opens it, Content is the dialog box,
// Title/Description are read out by screen readers, Cancel closes it without acting.
/** The dialog's root: owns the open/closed state. */
const AlertDialog = AlertDialogPrimitive.Root
/** The element (usually a button) that opens the dialog. */
const AlertDialogTrigger = AlertDialogPrimitive.Trigger

/** The dialog box itself, drawn centered over a dark overlay, outside the normal page flow (a "portal"). */
function AlertDialogContent({
  className,
  ...props
}: ComponentProps<typeof AlertDialogPrimitive.Content>) {
  return (
    <AlertDialogPrimitive.Portal>
      <AlertDialogPrimitive.Overlay className="fixed inset-0 z-50 bg-black/50" />
      <AlertDialogPrimitive.Content
        data-slot="alert-dialog-content"
        className={cn(
          'fixed top-1/2 left-1/2 z-50 grid max-h-[90dvh] w-[calc(100%-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 gap-4 overflow-y-auto rounded-lg border bg-background p-6 shadow-lg',
          className,
        )}
        {...props}
      />
    </AlertDialogPrimitive.Portal>
  )
}

/** The dialog's heading. Screen readers announce it first. */
function AlertDialogTitle({
  className,
  ...props
}: ComponentProps<typeof AlertDialogPrimitive.Title>) {
  return (
    <AlertDialogPrimitive.Title className={cn('text-lg font-semibold', className)} {...props} />
  )
}

/** Explains the decision. Screen readers announce it after the title. */
function AlertDialogDescription({
  className,
  ...props
}: ComponentProps<typeof AlertDialogPrimitive.Description>) {
  return (
    <AlertDialogPrimitive.Description
      className={cn('text-sm text-muted-foreground', className)}
      {...props}
    />
  )
}

/** A button that closes the dialog without doing anything. */
const AlertDialogCancel = AlertDialogPrimitive.Cancel

export {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogTitle,
  AlertDialogTrigger,
}
