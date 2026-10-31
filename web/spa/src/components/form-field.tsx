import { useId, type ReactNode } from 'react'
import { Label } from '@/components/ui/label'

/**
 * Accessibility attributes {@link FormField} hands to the input it wraps. Spread them on the input:
 * `<Input {...a11y} />`. They link the input to its label, hint and error for screen readers.
 */
export interface FieldA11yProps {
  id: string
  'aria-invalid': true | undefined
  'aria-describedby': string | undefined
}

/** Props for {@link FormField}. */
interface FormFieldProps {
  /** Visible text of the label. */
  label: string
  /** Error text to show under the field. Omit when the field is valid. */
  error?: string | undefined
  /** Help text shown under the field. */
  hint?: string
  /** Draws the actual control. Receives the id and `aria-*` props to put on it. */
  render: (props: FieldA11yProps) => ReactNode
}

/**
 * One label, one control, an optional hint, and an error that is linked to the control,
 * so screen readers read the error when the field gets focus. It is not a live region itself: a form-level
 * alert or the error summary announces the failure once, and focus moves to the first invalid field.
 *
 * Example: `<FormField label="Email" error={errors.email?.message} render={(a11y) => <Input {...a11y} />} />`
 */
export function FormField({ label, error, hint, render }: FormFieldProps) {
  const id = useId()
  const hintId = hint ? `${id}-hint` : undefined
  const errorId = error ? `${id}-error` : undefined
  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined

  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      {render({ id, 'aria-invalid': error ? true : undefined, 'aria-describedby': describedBy })}
      {hint && (
        <p id={hintId} className="text-sm text-muted-foreground">
          {hint}
        </p>
      )}
      {error && (
        <p id={errorId} className="text-sm font-medium text-destructive">
          {error}
        </p>
      )}
    </div>
  )
}
