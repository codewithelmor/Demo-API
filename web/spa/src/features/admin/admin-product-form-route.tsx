/** FR-F052, FR-F053: create a product, and replace one with If-Match. */
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate, useParams, type LoaderFunctionArgs } from 'react-router'
import { productKeys } from '@/api/products'
import { FormAlert } from '@/components/form-alert'
import { FormField } from '@/components/form-field'
import { SubmitRow } from '@/components/submit-row'
import { buttonVariants, Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import {
  adminProductQuery,
  createProduct,
  updateProduct,
  type AdminProductData,
} from '@/features/admin/admin-products-api'
import {
  productSchema,
  type ProductFormInput,
  type ProductFormValues,
} from '@/features/admin/admin-product-schema'
import { useServerFailure } from '@/hooks/use-server-failure'
import { apiErrorMessage } from '@/lib/error-message'
import { ApiError } from '@/lib/problem-details'

/** Starts reading the product before the edit page renders. */
export function adminProductLoader(queryClient: QueryClient) {
  return ({ params }: LoaderFunctionArgs) => {
    if (params.productId)
      queryClient.query(adminProductQuery(params.productId)).catch(() => undefined)
    return null
  }
}

/** The form's field names. */
const fieldNames = ['name', 'sku', 'description', 'price', 'stockQuantity'] as const

/** Empty form for a new product. */
const emptyValues: ProductFormInput = {
  name: '',
  sku: '',
  description: '',
  price: '',
  stockQuantity: '0',
}

/** Page for `/admin/products/new`. */
export function AdminProductCreateRoute() {
  return (
    <section className="grid max-w-xl gap-4">
      <title>New product · Back office · Orders Shop</title>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        New product
      </h1>
      <ProductForm />
    </section>
  )
}

/** Page for `/admin/products/:productId`: reads the product with its ETag, then shows the form. */
export function AdminProductEditRoute() {
  const { productId = '' } = useParams()
  const { data, error, isPending, isError, refetch, isFetching } = useQuery(
    adminProductQuery(productId),
  )
  return (
    <section className="grid max-w-xl gap-4">
      <title>Edit product · Back office · Orders Shop</title>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        Edit product
      </h1>
      {isPending && <p role="status">Loading product…</p>}
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
      {data && <ProductForm data={data} />}
    </section>
  )
}

/** Converts a saved product into form text values. */
function toInput(data: AdminProductData): ProductFormInput {
  const { product } = data
  return {
    name: product.name,
    sku: product.sku,
    description: product.description ?? '',
    price: product.price.toFixed(2),
    stockQuantity: String(product.stockQuantity),
  }
}

/**
 * Shared create/edit form. With `data` it edits: the save sends the ETag as If-Match. On a 412 (someone saved
 * first) the form is filled with the latest saved values and a notice says so, so the admin can re-apply
 * the change on top of the newest version.
 */
function ProductForm({ data }: { data?: AdminProductData }) {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const editing = data !== undefined
  const {
    register,
    handleSubmit,
    setError,
    setValue,
    formState: { errors },
  } = useForm<ProductFormInput, unknown, ProductFormValues>({
    resolver: zodResolver(productSchema),
    defaultValues: data ? toInput(data) : emptyValues,
  })
  const { failure, secondsLeft, locked, fail, clear } = useServerFailure(setError, fieldNames)
  const [etag, setEtag] = useState(data?.etag)
  const [conflicts, setConflicts] = useState(0)
  const conflictRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (conflicts > 0) conflictRef.current?.focus()
  }, [conflicts])

  const mutation = useMutation({
    mutationFn: (values: ProductFormValues) =>
      data ? updateProduct(data.product.id, etag, values) : createProduct(values),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: productKeys.all })
      await navigate('/admin/products')
    },
    onError: async (cause) => {
      if (data && cause instanceof ApiError && cause.status === 412) {
        try {
          const fresh = await queryClient.query({
            ...adminProductQuery(data.product.id),
            staleTime: 0,
          })
          const latest = toInput(fresh)
          for (const field of fieldNames) setValue(field, latest[field])
          setEtag(fresh.etag)
          setConflicts((count) => count + 1)
        } catch (reloadError) {
          fail(reloadError)
        }
        return
      }
      fail(cause)
    },
  })

  return (
    <>
      {conflicts > 0 && (
        <div
          ref={conflictRef}
          role="alert"
          tabIndex={-1}
          className="rounded-md border border-amber-600 p-3 text-sm outline-none"
        >
          <p className="font-medium">This product changed since you opened it.</p>
          <p>The form now shows the latest saved values. Apply your change again and save.</p>
        </div>
      )}
      <FormAlert failure={failure} />
      <form
        noValidate
        onSubmit={(event) => {
          clear()
          void handleSubmit((values) => {
            mutation.mutate(values)
          })(event)
        }}
        className="grid gap-4"
      >
        <FormField
          label="Name"
          error={errors.name?.message}
          render={(a11y) => <Input {...a11y} {...register('name')} />}
        />
        <FormField
          label="SKU"
          hint="Letters, digits and hyphens. Stored in upper case."
          error={errors.sku?.message}
          render={(a11y) => <Input autoCapitalize="characters" {...a11y} {...register('sku')} />}
        />
        <FormField
          label="Description (optional)"
          error={errors.description?.message}
          render={(a11y) => <Textarea {...a11y} {...register('description')} />}
        />
        <FormField
          label="Price"
          error={errors.price?.message}
          render={(a11y) => <Input inputMode="decimal" {...a11y} {...register('price')} />}
        />
        <FormField
          label="Stock quantity"
          error={errors.stockQuantity?.message}
          render={(a11y) => <Input inputMode="numeric" {...a11y} {...register('stockQuantity')} />}
        />
        <div className="flex flex-wrap items-center gap-3">
          <SubmitRow
            label={editing ? 'Save changes' : 'Create product'}
            pendingLabel="Saving…"
            pending={mutation.isPending}
            secondsLeft={locked ? secondsLeft : 0}
          />
          <Link to="/admin/products" className={buttonVariants({ variant: 'outline' })}>
            Cancel
          </Link>
        </div>
      </form>
    </>
  )
}
