import { useId, type ReactNode } from 'react'
import { Label } from '@/components/ui/label'

export interface FieldA11yProps {
  id: string
  'aria-invalid': true | undefined
  'aria-describedby': string | undefined
}

interface FormFieldProps {
  label: string
  error?: string | undefined
  hint?: string
  render: (props: FieldA11yProps) => ReactNode
}

// One label, one control, an optional hint, and an error that is linked to the control,
// so screen readers read the error when the field gets focus. It is not a live region itself: a form-level
// alert or the error summary announces the failure once, and focus moves to the first invalid field.
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
