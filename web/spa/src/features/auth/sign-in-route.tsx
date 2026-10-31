import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useNavigate, useSearchParams } from 'react-router'
import { FormField } from '@/components/form-field'
import { TextLink } from '@/components/text-link'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { UnconfirmedAlert } from '@/features/auth/unconfirmed-alert'
import { signInSchema, type SignInValues } from '@/features/auth/sign-in-schema'
import { returnTargetFor, signIn, startSession, type SessionUser } from '@/lib/session'
import { useCountdown } from '@/hooks/use-countdown'
import { ApiError, fieldErrors, networkError } from '@/lib/problem-details'
import { messageFor, rateLimitMessage, referenceFor } from '@/lib/problem-messages'
import { safeReturnUrl } from '@/lib/safe-url'

const reasonMessages: Record<string, string> = {
  'session-ended': 'Your session ended. Sign in again to continue.',
  'password-changed': 'Password changed. Sign in with your new password.',
  'signed-out': 'You signed out.',
  'account-erased': 'Your account was erased.',
}

function isFieldName(name: string): name is keyof SignInValues {
  return name === 'email' || name === 'password'
}

export function SignInRoute() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const reason = searchParams.get('reason')
  const returnUrl = safeReturnUrl(searchParams.get('returnUrl'))
  const [formError, setFormError] = useState<ApiError | undefined>()
  // Set after 403 email-not-confirmed: the typed email, reused by [Resend email] (WCAG 3.3.7).
  const [unconfirmedEmail, setUnconfirmedEmail] = useState<string | undefined>()

  const {
    register,
    handleSubmit,
    setError,
    setFocus,
    resetField,
    formState: { errors },
  } = useForm<SignInValues>({
    resolver: zodResolver(signInSchema),
    defaultValues: { email: '', password: '' },
  })

  const rateLimited = formError?.status === 429 ? formError : undefined
  const secondsLeft = useCountdown(rateLimited, rateLimited?.retryAfterSeconds ?? 0)
  const locked = rateLimited !== undefined && secondsLeft > 0

  const mutation = useMutation({
    mutationFn: signIn,
    onSuccess: async (user: SessionUser) => {
      startSession(queryClient, user)
      // Back to where the user was headed when their role may open it (wireframe AN-2), else the role home.
      await navigate(returnTargetFor(user, returnUrl), { replace: true })
    },
    onError: (error, credentials) => {
      // Anything that is not an API problem (offline, timeout) gets the generic network message.
      const failure = error instanceof ApiError ? error : networkError(error)
      setFormError(failure)
      if (failure.slug === 'email-not-confirmed') setUnconfirmedEmail(credentials.email)
      const mapped = fieldErrors(failure.problem)
      let focused = false
      for (const [name, message] of Object.entries(mapped)) {
        if (!isFieldName(name)) continue
        setError(name, { message }, { shouldFocus: !focused })
        focused = true
      }
      if (failure.slug === 'invalid-credentials') {
        // The password is cleared and focus moves to it (wireframe AN-2).
        resetField('password', { defaultValue: '' })
        setFocus('password')
      }
    },
  })

  const onSubmit = (values: SignInValues) => {
    setFormError(undefined)
    setUnconfirmedEmail(undefined)
    mutation.mutate(values)
  }

  // Own keys only: ?reason=constructor must not find anything on the prototype.
  const reasonMessage =
    reason !== null && Object.hasOwn(reasonMessages, reason) ? reasonMessages[reason] : undefined
  const reference = formError ? referenceFor(formError) : undefined

  return (
    <section className="mx-auto grid max-w-md gap-6 rounded-lg border p-6">
      <title>Sign in · Orders Shop</title>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        Sign in
      </h1>

      {reasonMessage && (
        <p role="status" className="rounded-md bg-muted p-3 text-sm">
          {reasonMessage}
        </p>
      )}

      {formError && unconfirmedEmail !== undefined && (
        <UnconfirmedAlert email={unconfirmedEmail} message={messageFor(formError)} />
      )}

      {formError && unconfirmedEmail === undefined && (
        <div role="alert" className="rounded-md border border-destructive p-3 text-sm">
          <p>{locked ? rateLimitMessage(secondsLeft) : messageFor(formError)}</p>
          {reference && <p className="mt-1 text-muted-foreground">Reference: {reference}</p>}
        </div>
      )}

      <form
        noValidate
        onSubmit={(event) => {
          void handleSubmit(onSubmit)(event)
        }}
        className="grid gap-4"
      >
        <FormField
          label="Email"
          error={errors.email?.message}
          render={(a11y) => (
            <Input type="email" autoComplete="username" {...a11y} {...register('email')} />
          )}
        />
        <FormField
          label="Password"
          error={errors.password?.message}
          render={(a11y) => (
            <Input
              type="password"
              autoComplete="current-password"
              {...a11y}
              {...register('password')}
            />
          )}
        />
        <p>
          <TextLink to="/forgot-password">Forgot password?</TextLink>
        </p>
        <div>
          <Button
            type="submit"
            disabled={mutation.isPending || locked}
            aria-busy={mutation.isPending}
          >
            {mutation.isPending ? 'Signing in…' : 'Sign in'}
          </Button>
        </div>
      </form>

      <div className="grid gap-1 text-sm">
        <p>
          New here? <TextLink to="/register">Create account</TextLink>
        </p>
        <p>
          Did not get the confirmation email?{' '}
          <TextLink to="/resend-confirmation">Resend it</TextLink>
        </p>
      </div>
    </section>
  )
}
