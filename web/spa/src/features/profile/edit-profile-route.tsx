import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { Link, useNavigate } from 'react-router'
import { FormAlert } from '@/components/form-alert'
import { FormField } from '@/components/form-field'
import { SubmitRow } from '@/components/submit-row'
import { Button, buttonVariants } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { AccountLayout } from '@/features/profile/account-nav'
import {
  profileKey,
  profileQuery,
  updateProfile,
  type Profile,
  type ProfileData,
} from '@/features/profile/profile-api'
import { profileSchema, type ProfileValues } from '@/features/profile/profile-schemas'
import { useServerFailure } from '@/hooks/use-server-failure'
import { apiErrorMessage } from '@/lib/error-message'
import { ApiError } from '@/lib/problem-details'

export function editProfileLoader(queryClient: QueryClient) {
  return () => {
    queryClient.query(profileQuery).catch(() => undefined)
    return null
  }
}

const fieldNames = ['firstName', 'lastName', 'phone'] as const
type FieldName = (typeof fieldNames)[number]

const toValues = (profile: Profile): ProfileValues => ({
  firstName: profile.firstName,
  lastName: profile.lastName,
  phone: profile.phone ?? '',
})

const labels: Record<FieldName, string> = {
  firstName: 'First name',
  lastName: 'Last name',
  phone: 'Phone',
}

// CU-8 (EP-35 to read, EP-36 to save)
export function EditProfileRoute() {
  const { data, error, isPending, isError, refetch, isFetching } = useQuery(profileQuery)

  return (
    <AccountLayout>
      <title>Edit profile · Orders Shop</title>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        Edit profile
      </h1>
      {isPending && <p role="status">Loading your profile…</p>}
      {isError && (
        <div
          role="alert"
          className="grid justify-items-start gap-3 rounded-md border border-destructive p-4"
        >
          <p>{apiErrorMessage(error)}</p>
          {!(error instanceof ApiError && error.status === 404) && (
            <Button
              variant="outline"
              disabled={isFetching}
              onClick={() => {
                void refetch()
              }}
            >
              {isFetching ? 'Trying again…' : 'Try again'}
            </Button>
          )}
        </div>
      )}
      {data && <EditForm data={data} />}
    </AccountLayout>
  )
}

interface SaveVariables {
  etag: string | undefined
  values: ProfileValues
}

function EditForm({ data }: { data: ProfileData }) {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const latest = toValues(data.profile)
  const {
    register,
    handleSubmit,
    setError,
    setValue,
    control,
    formState: { errors },
  } = useForm<ProfileValues>({ resolver: zodResolver(profileSchema), defaultValues: latest })
  const { failure, secondsLeft, locked, fail, clear } = useServerFailure(setError, fieldNames)
  const typed = useWatch({ control })

  // After a 412 (wireframe 0.3): what the user typed, kept in memory; the form shows the latest saved values.
  // Only the fields the user changed from what they opened are kept (baseline = the values when the form opened).
  const [mine, setMine] = useState<Partial<ProfileValues> | undefined>()
  const [baseline, setBaseline] = useState(latest)
  const [conflicts, setConflicts] = useState(0)
  const conflictRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (conflicts > 0) conflictRef.current?.focus()
  }, [conflicts])

  const mutation = useMutation({
    mutationFn: ({ etag, values }: SaveVariables) => updateProfile(etag, values),
    onSuccess: async (saved) => {
      // The response has the saved values and the new ETag; they replace the cached profile.
      queryClient.setQueryData(profileKey, saved)
      await navigate('/account', { state: { saved: true } })
    },
    onError: async (cause, variables) => {
      if (cause instanceof ApiError && cause.status === 412) {
        const changed: Partial<ProfileValues> = {}
        for (const field of fieldNames) {
          if (variables.values[field].trim() !== baseline[field].trim()) {
            changed[field] = variables.values[field]
          }
        }
        setMine(changed)
        try {
          // Load the latest record and its ETag; the next Save sends that one.
          const fresh = await queryClient.query({ ...profileQuery, staleTime: 0 })
          // setValue per field, not reset(): reset clears RHF's field registry, and with the React Compiler
          // the memoized register() refs are not called again, so the inputs would keep the typed text.
          const values = toValues(fresh.profile)
          for (const field of fieldNames) setValue(field, values[field])
          setBaseline(values)
          setConflicts((count) => count + 1)
        } catch (reloadError) {
          fail(reloadError)
        }
        return
      }
      if (cause instanceof ApiError && cause.slug === 'concurrency-conflict') {
        // A write race the ETag did not catch: show the message and read the record again.
        void queryClient.invalidateQueries({ queryKey: profileKey })
      }
      fail(cause)
    },
  })

  const onSubmit = (values: ProfileValues) => {
    clear()
    setMine(undefined)
    mutation.mutate({ etag: data.etag, values })
  }

  // Under a field: the user's own value, when it differs from the latest saved one and is not in the box.
  function suggestion(field: FieldName) {
    if (mine === undefined) return null
    const wanted = mine[field]?.trim()
    if (
      wanted === undefined ||
      wanted === latest[field].trim() ||
      wanted === (typed[field] ?? '').trim()
    )
      return null
    return (
      <div className="flex flex-wrap items-center gap-2 text-sm">
        <span>Your change: {wanted === '' ? '(empty)' : wanted}</span>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          aria-label={`Use my change for ${labels[field].toLowerCase()}`}
          className="min-h-6 min-w-6"
          onClick={() => {
            setValue(field, wanted, { shouldDirty: true })
          }}
        >
          Use my change
        </Button>
      </div>
    )
  }

  return (
    <>
      {conflicts > 0 && (
        <div
          ref={conflictRef}
          role="alert"
          tabIndex={-1}
          className="rounded-md border border-amber-600 p-3 text-sm outline-none"
        >
          <p className="font-medium">This record changed since you opened it.</p>
          <p>
            The form now shows the latest saved values. Your changes are shown under each field.
          </p>
        </div>
      )}
      <FormAlert failure={failure} />
      <form
        noValidate
        onSubmit={(event) => {
          void handleSubmit(onSubmit)(event)
        }}
        className="grid gap-4"
      >
        <div className="grid gap-1.5">
          <span className="text-sm font-medium">Email</span>
          <p>{data.profile.email}</p>
          <p className="text-sm text-muted-foreground">To change your email, contact support.</p>
        </div>
        <div className="grid gap-1">
          <FormField
            label="First name"
            error={errors.firstName?.message}
            render={(a11y) => (
              <Input autoComplete="given-name" {...a11y} {...register('firstName')} />
            )}
          />
          {suggestion('firstName')}
        </div>
        <div className="grid gap-1">
          <FormField
            label="Last name"
            error={errors.lastName?.message}
            render={(a11y) => (
              <Input autoComplete="family-name" {...a11y} {...register('lastName')} />
            )}
          />
          {suggestion('lastName')}
        </div>
        <div className="grid gap-1">
          <FormField
            label="Phone (optional)"
            hint="Use + and the country code, for example +14155550123. Clear the box to remove your phone number."
            error={errors.phone?.message}
            render={(a11y) => (
              <Input type="tel" autoComplete="tel" {...a11y} {...register('phone')} />
            )}
          />
          {suggestion('phone')}
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <SubmitRow
            label="Save changes"
            pendingLabel="Saving…"
            pending={mutation.isPending}
            secondsLeft={locked ? secondsLeft : 0}
          />
          <Link to="/account" className={buttonVariants({ variant: 'outline' })}>
            Cancel
          </Link>
        </div>
      </form>
    </>
  )
}
