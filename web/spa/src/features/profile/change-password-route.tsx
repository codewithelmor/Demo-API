import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useForm, useWatch } from 'react-hook-form'
import { FormAlert } from '@/components/form-alert'
import { FormField } from '@/components/form-field'
import { PasswordInput } from '@/components/password-input'
import { SubmitRow } from '@/components/submit-row'
import { AccountLayout } from '@/features/profile/account-nav'
import { changePassword } from '@/features/profile/profile-api'
import { changePasswordSchema, type ChangePasswordValues } from '@/features/profile/profile-schemas'
import { useEndSession } from '@/hooks/use-end-session'
import { useServerFailure } from '@/hooks/use-server-failure'
import { codePointLength, PASSWORD_MAX, PASSWORD_MIN } from '@/lib/field-rules'

const WRONG_PASSWORD =
  'Current password is incorrect. After 5 failed attempts your account is locked for 15 minutes.'

// CU-9 (EP-09)
export function ChangePasswordRoute() {
  const endSession = useEndSession()
  const {
    register,
    handleSubmit,
    setError,
    control,
    formState: { errors, touchedFields },
  } = useForm<ChangePasswordValues>({
    resolver: zodResolver(changePasswordSchema),
    mode: 'onTouched',
    defaultValues: { currentPassword: '', newPassword: '' },
  })
  const { failure, secondsLeft, locked, fail, clear } = useServerFailure(setError, [
    'currentPassword',
    'newPassword',
  ])
  const newPassword = useWatch({ control, name: 'newPassword' })

  const mutation = useMutation({
    mutationFn: changePassword,
    // The API revoked every token and the BFF ended the session. The SPA drops its cached data and goes
    // to sign-in. The cart stays: a session that just ends keeps it (UX-D2).
    onSuccess: () => endSession('password-changed', { clearCart: false }),
    onError: (error) => {
      fail(error)
    },
  })

  const onSubmit = (values: ChangePasswordValues) => {
    clear()
    mutation.mutate(values)
  }

  const hint = touchedFields.newPassword
    ? `At least ${String(PASSWORD_MIN)} characters. A short sentence works well. Spaces are allowed. ${String(codePointLength(newPassword))} of ${String(PASSWORD_MIN)}-${String(PASSWORD_MAX)} characters.`
    : `At least ${String(PASSWORD_MIN)} characters. A short sentence works well. Spaces are allowed.`

  // The server's field error for the current password is the "wrong password" case (it counts toward the lockout).
  const currentError =
    errors.currentPassword?.type === 'server' ? WRONG_PASSWORD : errors.currentPassword?.message

  return (
    <AccountLayout>
      <title>Change password · Orders Shop</title>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        Change password
      </h1>
      <p>After you change it, you will be signed out on all devices.</p>
      <FormAlert failure={failure} />
      <form
        noValidate
        onSubmit={(event) => {
          void handleSubmit(onSubmit)(event)
        }}
        className="grid gap-4"
      >
        <FormField
          label="Current password"
          error={currentError}
          render={(a11y) => (
            <PasswordInput
              autoComplete="current-password"
              {...a11y}
              {...register('currentPassword')}
            />
          )}
        />
        <FormField
          label="New password"
          hint={hint}
          error={errors.newPassword?.message}
          render={(a11y) => (
            <PasswordInput autoComplete="new-password" {...a11y} {...register('newPassword')} />
          )}
        />
        <SubmitRow
          label="Change password"
          pendingLabel="Changing…"
          pending={mutation.isPending}
          secondsLeft={locked ? secondsLeft : 0}
        />
      </form>
    </AccountLayout>
  )
}
