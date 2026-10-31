import { Button } from '@/components/ui/button'

/** Props for {@link SubmitRow}. */
interface SubmitRowProps {
  /** Button text normally. */
  label: string
  /** Button text while the request is running. */
  pendingLabel: string
  /** True while the request is running. */
  pending: boolean
  /** Seconds left of a 429 Retry-After; the button stays disabled while this is above 0 (NFR-F23). */
  secondsLeft?: number
}

/**
 * The primary action: disabled while the request runs (no double submit, NFR-F24) and during a
 * 429 countdown. The countdown text is for sighted users; the alert already announced the 429.
 * A press on the button does not take focus from the field (preventDefault on mousedown). Otherwise the
 * field's blur check shows its error, the form grows, the button moves away under the pointer and the
 * click is lost: the user must click twice. Submitting validates every field anyway.
 */
export function SubmitRow({ label, pendingLabel, pending, secondsLeft = 0 }: SubmitRowProps) {
  return (
    <div className="flex flex-wrap items-center gap-3">
      <Button
        type="submit"
        disabled={pending || secondsLeft > 0}
        aria-busy={pending}
        onMouseDown={(event) => {
          event.preventDefault()
        }}
      >
        {pending ? pendingLabel : label}
      </Button>
      {secondsLeft > 0 && (
        <span aria-hidden="true" className="text-sm text-muted-foreground">
          You can try again in {secondsLeft} s
        </span>
      )}
    </div>
  )
}
