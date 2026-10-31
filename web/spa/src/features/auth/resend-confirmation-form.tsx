import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { FormAlert } from '@/components/form-alert'
import { FormField } from '@/components/form-field'
import { SubmitRow } from '@/components/submit-row'
import { Input } from '@/components/ui/input'
import { emailSchema, type EmailValues } from '@/features/auth/access-schemas'
import { resendConfirmation } from '@/features/auth/access-api'
import { useServerFailure } from '@/hooks/use-server-failure'

/** Neutral result text, the same for every email. */
export const RESEND_RESULT = 'If an account needs confirming, we sent a new link.'

/** Props for {@link ResendConfirmationForm}. */
interface ResendConfirmationFormProps {
  /** Text of the submit button. */
  submitLabel: string
  /** Email to prefill, when it is already known. */
  defaultEmail?: string
}

/**
 * EP-03. The result is the same for every email (anti-enumeration), so the page never says whether
// the account exists. Used on /resend-confirmation and inline on /confirm-email.
 *
 * Form handling: `useForm` (React Hook Form) tracks the fields, `zodResolver` validates them with the Zod
 * schema, and `useMutation` sends the request.
 */
export function ResendConfirmationForm({
  submitLabel,
  defaultEmail = '',
}: ResendConfirmationFormProps) {
  const [sent, setSent] = useState(false)
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<EmailValues>({
    resolver: zodResolver(emailSchema),
    mode: 'onTouched',
    defaultValues: { email: defaultEmail },
  })
  const { failure, secondsLeft, locked, fail, clear } = useServerFailure(setError, ['email'])

  const mutation = useMutation({
    mutationFn: resendConfirmation,
    onSuccess: () => {
      setSent(true)
    },
    onError: fail,
  })

  const onSubmit = (values: EmailValues) => {
    clear()
    setSent(false)
    mutation.mutate(values.email)
  }

  return (
    <form
      noValidate
      onSubmit={(event) => {
        void handleSubmit(onSubmit)(event)
      }}
      className="grid gap-4"
    >
      <FormAlert failure={failure} />
      <FormField
        label="Email"
        error={errors.email?.message}
        render={(a11y) => (
          <Input type="email" autoComplete="email" {...a11y} {...register('email')} />
        )}
      />
      <SubmitRow
        label={submitLabel}
        pendingLabel="Sending…"
        pending={mutation.isPending}
        secondsLeft={locked ? secondsLeft : 0}
      />
      {/* Always in the page, so the result is announced when its text appears. */}
      <p role="status" className="text-sm">
        {sent ? RESEND_RESULT : ''}
      </p>
    </form>
  )
}
