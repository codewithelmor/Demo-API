import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { z } from '@/lib/zod'
import { ORDER_STATUSES, type OrderStatus } from '@/api/orders'
import { FormField } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { OrdersParams } from '@/features/orders/orders-params'
import { isValidDay } from '@/lib/dates'

/** A day typed in a date box: empty or `YYYY-MM-DD`. */
const dayText = z
  .string()
  .refine((value) => value === '' || isValidDay(value), { error: 'Enter a valid date.' })

/** To before From is caught here, so no request is made (US-F030 AC2). Days are yyyy-mm-dd, so they compare as text. */
const filtersSchema = z
  .object({ statuses: z.array(z.enum(ORDER_STATUSES)), from: dayText, to: dayText })
  .refine((values) => values.from === '' || values.to === '' || values.to >= values.from, {
    path: ['to'],
    error: 'The To date must be on or after the From date.',
  })

/** What the filter form holds while the user edits it. */
type FiltersValues = z.infer<typeof filtersSchema>

/** Props for {@link OrdersFilters}. */
interface OrdersFiltersProps {
  params: OrdersParams
  onApply: (filters: Pick<OrdersParams, 'statuses' | 'from' | 'to'>) => void
  onClear: () => void
  canClear: boolean
}

/**
 * Status checkboxes and From / To dates (wireframe CU-4). Applied with a button. The parent remounts
 * this form (key) when the URL changes the values.
 */
export function OrdersFilters({ params, onApply, onClear, canClear }: OrdersFiltersProps) {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FiltersValues>({
    resolver: zodResolver(filtersSchema),
    defaultValues: { statuses: params.statuses, from: params.from ?? '', to: params.to ?? '' },
  })

  const onSubmit = (values: FiltersValues) => {
    const statuses: OrderStatus[] = values.statuses
    onApply({
      statuses,
      from: values.from === '' ? undefined : values.from,
      to: values.to === '' ? undefined : values.to,
    })
  }

  return (
    <form
      noValidate
      aria-label="Filters"
      onSubmit={(event) => {
        void handleSubmit(onSubmit)(event)
      }}
      className="flex flex-wrap items-start gap-4"
    >
      <fieldset className="grid gap-1.5">
        <legend className="mb-1.5 text-sm font-medium">Status</legend>
        <div className="flex flex-wrap gap-x-4 gap-y-2">
          {ORDER_STATUSES.map((status) => (
            <label key={status} className="flex min-h-6 items-center gap-2 text-sm">
              <input
                type="checkbox"
                value={status}
                className="size-5 accent-primary"
                {...register('statuses')}
              />
              {status}
            </label>
          ))}
        </div>
      </fieldset>
      <div className="w-44">
        <FormField
          label="From"
          error={errors.from?.message}
          render={(a11y) => <Input type="date" {...a11y} {...register('from')} />}
        />
      </div>
      <div className="w-44">
        <FormField
          label="To"
          error={errors.to?.message}
          render={(a11y) => <Input type="date" {...a11y} {...register('to')} />}
        />
      </div>
      <div className="flex gap-2 self-end">
        <Button type="submit">Apply filters</Button>
        {canClear && (
          <Button type="button" variant="ghost" onClick={onClear}>
            Clear filters
          </Button>
        )}
      </div>
    </form>
  )
}
