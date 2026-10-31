import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useForm, useWatch } from 'react-hook-form'
import { Link, useLoaderData, useNavigate } from 'react-router'
import { FormAlert } from '@/components/form-alert'
import { FormField } from '@/components/form-field'
import { PasswordInput } from '@/components/password-input'
import { SubmitRow } from '@/components/submit-row'
import { TextLink } from '@/components/text-link'
import { buttonVariants } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { resetPassword } from '@/features/auth/access-api'
import {
  codePointLength,
  PASSWORD_MAX,
  PASSWORD_MIN,
  resetPasswordSchema,
  type ResetPasswordValues,
} from '@/features/auth/access-schemas'
import { useServerFailure } from '@/hooks/use-server-failure'

/** What the reset link carries. */
interface ResetLink {
  email: string
  token: string
}

/**
 * Route loader: reads email and token from the link once, then removes them from the address bar and the
 * history entry (AN-8, US-F025 AC3). The values live only in the loader data (memory) after that.
 * A refresh then finds no token, and the page tells the user to open the link again.
 *
 * @returns The email and token, or `null` when the link has no (or half) data.
 */
export function resetPasswordLoader({ request }: { request: Request }): ResetLink | null {
  const params = new URL(request.url).searchParams
  const email = params.get('email')
  const token = params.get('token')
  if (window.location.search !== '') {
    window.history.replaceState(window.history.state, '', window.location.pathname)
  }
  return email && token ? { email, token } : null
}

/** AN-8 (EP-11): the "set a new password" page that the reset email links to. */
export function ResetPasswordRoute() {
  const link = useLoaderData<ResetLink | null>()
  return (
    <section className="mx-auto grid max-w-md gap-6 rounded-lg border p-6">
      <title>Set a new password · Orders Shop</title>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        Set a new password
      </h1>
      {link ? <ResetForm link={link} /> : <MissingLink />}
    </section>
  )
}

/** Button to the forgot-password page, shown when the link cannot be used. */
function RequestNewLink() {
  return (
    <div>
      <Link to="/forgot-password" className={buttonVariants()}>
        Request new link
      </Link>
    </div>
  )
}

/** Shown when the page is opened without a link (or after a refresh, which removed it). */
function MissingLink() {
  return (
    <>
      <p role="status">Open the link from your email again, or request a new link.</p>
      <RequestNewLink />
    </>
  )
}

/** The form with the new-password field. The email is shown read-only so password managers can pair it. */
function ResetForm({ link }: { link: ResetLink }) {
  const navigate = useNavigate()
  const {
    register,
    handleSubmit,
    setError,
    control,
    formState: { errors, touchedFields },
  } = useForm<ResetPasswordValues>({
    resolver: zodResolver(resetPasswordSchema),
    mode: 'onTouched',
    defaultValues: { newPassword: '' },
  })
  const { failure, secondsLeft, locked, fail, clear } = useServerFailure(setError, ['newPassword'])
  const newPassword = useWatch({ control, name: 'newPassword' })

  const mutation = useMutation({
    mutationFn: resetPassword,
    onSuccess: async () => {
      // "Password changed. Sign in with your new password." is the sign-in page's reason text.
      await navigate('/sign-in?reason=password-changed', { replace: true })
    },
    onError: (error) => {
      fail(error)
    },
  })

  const onSubmit = (values: ResetPasswordValues) => {
    clear()
    mutation.mutate({ email: link.email, token: link.token, newPassword: values.newPassword })
  }

  if (failure?.error.slug === 'invalid-token') {
    return (
      <>
        <div role="alert" className="rounded-md border border-destructive p-3 text-sm">
          <p>This link is invalid or expired.</p>
        </div>
        <RequestNewLink />
      </>
    )
  }

  const hint = touchedFields.newPassword
    ? `At least ${String(PASSWORD_MIN)} characters. A short sentence works well. Spaces are allowed. ${String(codePointLength(newPassword))} of ${String(PASSWORD_MIN)}-${String(PASSWORD_MAX)} characters.`
    : `At least ${String(PASSWORD_MIN)} characters. A short sentence works well. Spaces are allowed.`

  return (
    <>
      <FormAlert failure={failure} />
      <form
        noValidate
        onSubmit={(event) => {
          void handleSubmit(onSubmit)(event)
        }}
        className="grid gap-4"
      >
        <FormField
          label="Email"
          render={(a11y) => (
            <Input type="email" readOnly autoComplete="username" value={link.email} {...a11y} />
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
          label="Set new password"
          pendingLabel="Saving…"
          pending={mutation.isPending}
          secondsLeft={locked ? secondsLeft : 0}
        />
      </form>
      <p>
        <TextLink to="/sign-in">Back to sign in</TextLink>
      </p>
    </>
  )
}
