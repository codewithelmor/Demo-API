import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { FormAlert } from '@/components/form-alert'
import { FormField } from '@/components/form-field'
import { SubmitRow } from '@/components/submit-row'
import { TextLink } from '@/components/text-link'
import { Input } from '@/components/ui/input'
import { forgotPassword } from '@/features/auth/access-api'
import { emailSchema, type EmailValues } from '@/features/auth/access-schemas'
import { useServerFailure } from '@/hooks/use-server-failure'

export const FORGOT_RESULT =
  'If an account exists for this email, we sent a link. It works for 1 hour.'

// AN-7 (EP-10). The same result for every email, valid or not (anti-enumeration).
export function ForgotPasswordRoute() {
  const [sent, setSent] = useState(false)
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<EmailValues>({
    resolver: zodResolver(emailSchema),
    mode: 'onTouched',
    defaultValues: { email: '' },
  })
  const { failure, secondsLeft, locked, fail, clear } = useServerFailure(setError, ['email'])

  const mutation = useMutation({
    mutationFn: forgotPassword,
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
    <section className="mx-auto grid max-w-md gap-6 rounded-lg border p-6">
      <title>Reset your password · Orders Shop</title>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        Reset your password
      </h1>
      <p className="text-sm text-muted-foreground">
        Enter your email and we will send you a link to choose a new password.
      </p>
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
          label="Send reset link"
          pendingLabel="Sending…"
          pending={mutation.isPending}
          secondsLeft={locked ? secondsLeft : 0}
        />
        <p role="status" className="text-sm">
          {sent ? FORGOT_RESULT : ''}
        </p>
      </form>
      <p>
        <TextLink to="/sign-in">Back to sign in</TextLink>
      </p>
    </section>
  )
}
