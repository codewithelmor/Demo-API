import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm, useWatch, type FieldErrors, type Path } from 'react-hook-form'
import { useNavigate } from 'react-router'
import { FormAlert } from '@/components/form-alert'
import { FormField } from '@/components/form-field'
import { PasswordInput } from '@/components/password-input'
import { SubmitRow } from '@/components/submit-row'
import { TextLink } from '@/components/text-link'
import { Input } from '@/components/ui/input'
import { registerAccount } from '@/features/auth/access-api'
import {
  codePointLength,
  PASSWORD_MAX,
  PASSWORD_MIN,
  registerSchema,
  type RegisterValues,
} from '@/features/auth/access-schemas'
import { useServerFailure } from '@/hooks/use-server-failure'

/** The form's field names, in on-screen order (used to map server errors and build the error summary). */
const fieldNames: readonly Path<RegisterValues>[] = [
  'firstName',
  'lastName',
  'email',
  'password',
  'phone',
]

/** Visible name of each field, for the error summary. */
const fieldLabels: Record<Path<RegisterValues>, string> = {
  firstName: 'First name',
  lastName: 'Last name',
  email: 'Email',
  password: 'Password',
  phone: 'Phone',
}

/** One line of the "Fix N problems" summary: which field and what is wrong. */
interface SummaryItem {
  name: Path<RegisterValues>
  message: string
}

/** Builds the error summary from React Hook Form's errors, in on-screen order. */
function summaryOf(errors: FieldErrors<RegisterValues>): SummaryItem[] {
  return fieldNames.flatMap((name) => {
    const message = errors[name]?.message
    return message === undefined ? [] : [{ name, message }]
  })
}

/**
 * AN-3 (EP-01): the create-account page (`/register`). Success always goes to "Check your email", also for
 * an email that already exists, so the page never reveals which emails have accounts.
 * When the user submits with errors, a summary lists them and each item focuses its field.
 *
 * Forms here use three libraries together: React Hook Form (`useForm`) tracks the fields, a Zod schema
 * (through `zodResolver`) validates them, and TanStack Query's `useMutation` sends the request.
 */
export function RegisterRoute() {
  const navigate = useNavigate()
  const [summary, setSummary] = useState<SummaryItem[]>([])
  const {
    register,
    handleSubmit,
    setError,
    setFocus,
    control,
    formState: { errors, touchedFields },
  } = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    // Check a field when the user leaves it, then on every keystroke once it was touched.
    mode: 'onTouched',
    defaultValues: { firstName: '', lastName: '', email: '', password: '', phone: '' },
  })
  const { failure, secondsLeft, locked, fail, clear } = useServerFailure(setError, fieldNames)
  const password = useWatch({ control, name: 'password' })

  const mutation = useMutation({
    mutationFn: registerAccount,
    onSuccess: async () => {
      await navigate('/register/check-email', { replace: true })
    },
    onError: (error) => {
      fail(error)
    },
  })

  const onSubmit = (values: RegisterValues) => {
    clear()
    setSummary([])
    mutation.mutate(values)
  }

  const passwordHint = touchedFields.password
    ? `At least ${String(PASSWORD_MIN)} characters. A short sentence works well. Spaces are allowed. ${String(codePointLength(password))} of ${String(PASSWORD_MIN)}-${String(PASSWORD_MAX)} characters.`
    : `At least ${String(PASSWORD_MIN)} characters. A short sentence works well. Spaces are allowed.`

  return (
    <section className="mx-auto grid max-w-md gap-6 rounded-lg border p-6">
      <title>Create your account · Orders Shop</title>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        Create your account
      </h1>
      <p className="text-sm text-muted-foreground">
        All fields are required unless marked optional.
      </p>

      <FormAlert failure={failure} />

      {summary.length > 0 && (
        <div role="alert" className="rounded-md border border-destructive p-3 text-sm">
          <p className="font-medium">
            Fix {summary.length} {summary.length === 1 ? 'problem' : 'problems'} to continue
          </p>
          <ul className="mt-1 list-disc pl-5">
            {summary.map((item) => (
              <li key={item.name}>
                <button
                  type="button"
                  className="inline-block min-h-6 text-left underline underline-offset-4"
                  onClick={() => {
                    setFocus(item.name)
                  }}
                >
                  {fieldLabels[item.name]}: {item.message}
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      <form
        noValidate
        onSubmit={(event) => {
          void handleSubmit(onSubmit, (invalid) => {
            setSummary(summaryOf(invalid))
          })(event)
        }}
        className="grid gap-4"
      >
        <FormField
          label="First name"
          error={errors.firstName?.message}
          render={(a11y) => (
            <Input autoComplete="given-name" {...a11y} {...register('firstName')} />
          )}
        />
        <FormField
          label="Last name"
          error={errors.lastName?.message}
          render={(a11y) => (
            <Input autoComplete="family-name" {...a11y} {...register('lastName')} />
          )}
        />
        <FormField
          label="Email"
          error={errors.email?.message}
          render={(a11y) => (
            <Input type="email" autoComplete="email" {...a11y} {...register('email')} />
          )}
        />
        <FormField
          label="Password"
          hint={passwordHint}
          error={errors.password?.message}
          render={(a11y) => (
            <PasswordInput autoComplete="new-password" {...a11y} {...register('password')} />
          )}
        />
        <FormField
          label="Phone (optional)"
          hint="Include the country code, for example +14155550123."
          error={errors.phone?.message}
          render={(a11y) => (
            <Input type="tel" autoComplete="tel" {...a11y} {...register('phone')} />
          )}
        />
        <SubmitRow
          label="Create account"
          pendingLabel="Creating account…"
          pending={mutation.isPending}
          secondsLeft={locked ? secondsLeft : 0}
        />
      </form>

      <p className="text-sm">
        Already have an account? <TextLink to="/sign-in">Sign in</TextLink>
      </p>
    </section>
  )
}
