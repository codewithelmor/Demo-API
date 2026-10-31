import { zodResolver } from '@hookform/resolvers/zod'
import { useId } from 'react'
import { useForm } from 'react-hook-form'
import { z } from '@/lib/zod'
import { FormField } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { PRICE_PATTERN, type CatalogParams } from '@/features/catalog/catalog-params'

const priceText = z
  .string()
  .trim()
  .refine((value) => value === '' || PRICE_PATTERN.test(value), {
    error: 'Enter a price like 12 or 12.50.',
  })

// Min above max is caught here, so no request is made (US-F026 AC3).
const filtersSchema = z
  .object({ minPrice: priceText, maxPrice: priceText, inStock: z.boolean() })
  .refine(
    (values) =>
      values.minPrice === '' ||
      values.maxPrice === '' ||
      Number(values.minPrice) <= Number(values.maxPrice),
    { path: ['minPrice'], error: 'Min price must be less than or equal to max price.' },
  )

type FiltersValues = z.infer<typeof filtersSchema>

interface CatalogFiltersProps {
  params: CatalogParams
  onApply: (filters: Pick<CatalogParams, 'minPrice' | 'maxPrice' | 'inStock'>) => void
  onClear: () => void
  canClear: boolean
}

// Price range and "in stock only" (wireframe CU-1). Applied with a button, so a half-typed range
// never reaches the API. The parent remounts this form (key) when the URL changes the values.
export function CatalogFilters({ params, onApply, onClear, canClear }: CatalogFiltersProps) {
  const checkboxId = useId()
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FiltersValues>({
    resolver: zodResolver(filtersSchema),
    defaultValues: {
      minPrice: params.minPrice === undefined ? '' : String(params.minPrice),
      maxPrice: params.maxPrice === undefined ? '' : String(params.maxPrice),
      inStock: params.inStock,
    },
  })

  const onSubmit = (values: FiltersValues) => {
    onApply({
      minPrice: values.minPrice === '' ? undefined : Number(values.minPrice),
      maxPrice: values.maxPrice === '' ? undefined : Number(values.maxPrice),
      inStock: values.inStock,
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
      <div className="w-32">
        <FormField
          label="Min price"
          error={errors.minPrice?.message}
          render={(a11y) => (
            <Input inputMode="decimal" autoComplete="off" {...a11y} {...register('minPrice')} />
          )}
        />
      </div>
      <div className="w-32">
        <FormField
          label="Max price"
          error={errors.maxPrice?.message}
          render={(a11y) => (
            <Input inputMode="decimal" autoComplete="off" {...a11y} {...register('maxPrice')} />
          )}
        />
      </div>
      <div className="flex min-h-10 items-center gap-2 self-end">
        <input
          id={checkboxId}
          type="checkbox"
          className="size-5 accent-primary"
          {...register('inStock')}
        />
        <Label htmlFor={checkboxId}>In stock only</Label>
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
