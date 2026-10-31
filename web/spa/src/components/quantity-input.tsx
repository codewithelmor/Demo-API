import { Minus, Plus } from 'lucide-react'
import { useState } from 'react'
import { FormField } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

/** Props for {@link QuantityInput}. */
interface QuantityInputProps {
  /** Visible label, also used for the button names ("Decrease <label>"). */
  label: string
  /** The current quantity, owned by the parent (this is a "controlled" component). */
  value: number
  /** Highest allowed quantity (1000, or less when stock is lower). */
  max: number
  /** Called only with a valid quantity from 1 to max. */
  onChange: (quantity: number) => void
  /** Disables the buttons and the field. */
  disabled?: boolean
}

/**
 * A whole number from 1 to max, with minus and plus buttons. An invalid entry shows an error
 * as soon as it is typed and is never passed on (US-F028 AC3). A typed number is saved when the
 * field loses focus or on Enter, so half-typed numbers like "10" on the way to "1001" are not saved.
 */
export function QuantityInput({ label, value, max, onChange, disabled }: QuantityInputProps) {
  // What is typed in the field (text, because the user may type something invalid).
  const [text, setText] = useState(String(value))
  // The last `value` we showed, to notice when the parent changes it.
  const [shownValue, setShownValue] = useState(value)

  // The quantity changed from outside (for example "Set to 1"): show it.
  if (shownValue !== value) {
    setShownValue(value)
    setText(String(value))
  }

  /** Turns typed text into a valid quantity, or `undefined` when it is not a whole number from 1 to max. */
  function parse(entry: string): number | undefined {
    const quantity = /^\d+$/.test(entry.trim()) ? Number(entry) : Number.NaN
    return Number.isInteger(quantity) && quantity >= 1 && quantity <= max ? quantity : undefined
  }

  /** Accepts a valid quantity: updates the field and tells the parent when it really changed. */
  function save(quantity: number) {
    setText(String(quantity))
    if (quantity !== value) {
      setShownValue(quantity)
      onChange(quantity)
    }
  }

  const error = parse(text) === undefined ? `Enter a quantity from 1 to ${String(max)}.` : undefined
  const current = parse(text) ?? value

  /** Saves the typed text if it is valid (called on blur and Enter). */
  function commitTyped() {
    const quantity = parse(text)
    if (quantity !== undefined) save(quantity)
  }

  return (
    <FormField
      label={label}
      error={error}
      render={(a11y) => (
        <div className="flex items-center gap-2">
          <Button
            type="button"
            variant="outline"
            size="icon"
            aria-label={`Decrease ${label.toLowerCase()}`}
            disabled={disabled || current <= 1}
            onClick={() => {
              save(Math.max(1, current - 1))
            }}
          >
            <Minus aria-hidden="true" size={16} />
          </Button>
          <Input
            {...a11y}
            type="text"
            inputMode="numeric"
            autoComplete="off"
            className="w-20 text-center"
            value={text}
            disabled={disabled}
            onChange={(event) => {
              setText(event.target.value)
            }}
            onBlur={commitTyped}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                event.preventDefault()
                commitTyped()
              }
            }}
          />
          <Button
            type="button"
            variant="outline"
            size="icon"
            aria-label={`Increase ${label.toLowerCase()}`}
            disabled={disabled || current >= max}
            onClick={() => {
              save(Math.min(max, current + 1))
            }}
          >
            <Plus aria-hidden="true" size={16} />
          </Button>
        </div>
      )}
    />
  )
}
