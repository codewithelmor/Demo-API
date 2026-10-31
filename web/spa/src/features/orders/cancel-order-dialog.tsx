import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { cancelOrder, orderKeys } from '@/api/orders'
import { productKeys } from '@/api/products'
import { FormField } from '@/components/form-field'
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { useCountdown } from '@/hooks/use-countdown'
import { ApiError, networkError } from '@/lib/problem-details'
import { messageFor, referenceFor } from '@/lib/problem-messages'

/** Longest cancellation reason the API accepts. */
const REASON_MAX = 500

/** Props for {@link CancelOrderDialog}. */
interface CancelOrderDialogProps {
  orderId: string
  // Called when the API says the order cannot be cancelled any more (409), with the text to show.
  onConflict: (message: string) => void
}

/**
 * CU-6. Cancel your own Pending order, with an optional reason of up to 500 characters (EP-48).
 * A confirmation dialog: the user may type a reason, then confirms. After success the order is re-read
 * and the stock of its products is refreshed.
 */
export function CancelOrderDialog({ orderId, onConflict }: CancelOrderDialogProps) {
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [reason, setReason] = useState('')
  const [failure, setFailure] = useState<ApiError | undefined>()
  const rateLimited = failure?.status === 429 ? failure : undefined
  const secondsLeft = useCountdown(rateLimited, rateLimited?.retryAfterSeconds ?? 0)

  // Read the order again, so the page shows what the server now says.
  const reload = () => queryClient.invalidateQueries({ queryKey: orderKeys.all })
  // A cancelled order puts its items back into stock, so cached product stock is stale too.
  const reloadProducts = () => queryClient.invalidateQueries({ queryKey: productKeys.all })

  const mutation = useMutation({
    mutationFn: (text: string) => cancelOrder(orderId, text),
    onSuccess: async () => {
      // Stay "pending" until the order is read again, so the new status is on screen when the dialog closes.
      void reloadProducts()
      await reload()
      setOpen(false)
    },
    onError: async (cause) => {
      const error = cause instanceof ApiError ? cause : networkError(cause)
      if (error.slug === 'invalid-status-transition') {
        const status = error.problem.currentStatus
        onConflict(
          status
            ? `This order can no longer be cancelled (status: ${status}).`
            : 'This order can no longer be cancelled.',
        )
        await reload()
        setOpen(false)
        return
      }
      if (error.status === 404) {
        await reload() // the page then shows "Order not found"
        setOpen(false)
        return
      }
      if (error.slug === 'concurrency-conflict') void reload()
      setFailure(error)
    },
  })

  const tooLong = reason.length > REASON_MAX
  const locked = rateLimited !== undefined && secondsLeft > 0

  function changeOpen(next: boolean) {
    // Do not close while the request runs.
    if (!next && mutation.isPending) return
    setOpen(next)
    if (next) {
      setReason('')
      setFailure(undefined)
    }
  }

  function confirm() {
    if (mutation.isPending || tooLong) return
    setFailure(undefined)
    mutation.mutate(reason)
  }

  const reference = failure ? referenceFor(failure) : undefined

  return (
    <AlertDialog open={open} onOpenChange={changeOpen}>
      <AlertDialogTrigger asChild>
        <Button type="button" variant="destructive">
          Cancel order
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogTitle>Cancel this order?</AlertDialogTitle>
        <AlertDialogDescription>
          The items go back into stock. You cannot undo this.
        </AlertDialogDescription>

        {failure && (
          <div role="alert" className="rounded-md border border-destructive p-3 text-sm">
            <p>{messageFor(failure)}</p>
            {reference && <p className="mt-1 text-muted-foreground">Reference: {reference}</p>}
          </div>
        )}

        <FormField
          label="Reason (optional)"
          error={tooLong ? `Use ${String(REASON_MAX)} characters or fewer.` : undefined}
          hint={`${String(reason.length)}/${String(REASON_MAX)} characters`}
          render={(a11y) => (
            <Textarea
              {...a11y}
              value={reason}
              onChange={(event) => {
                setReason(event.target.value)
              }}
            />
          )}
        />

        <div className="flex flex-wrap justify-end gap-2">
          <AlertDialogCancel asChild>
            <Button type="button" variant="outline" disabled={mutation.isPending}>
              Keep order
            </Button>
          </AlertDialogCancel>
          <Button
            type="button"
            variant="destructive"
            disabled={mutation.isPending || tooLong || locked}
            aria-busy={mutation.isPending}
            onClick={confirm}
          >
            {mutation.isPending ? 'Cancelling…' : 'Cancel order'}
          </Button>
          {locked && (
            <span aria-hidden="true" className="self-center text-sm text-muted-foreground">
              You can try again in {secondsLeft} s
            </span>
          )}
        </div>
      </AlertDialogContent>
    </AlertDialog>
  )
}
