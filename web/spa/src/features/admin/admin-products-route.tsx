/** FR-F051, FR-F054: the admin product list, with delete and restore. */
import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useSearchParams, type LoaderFunctionArgs } from 'react-router'
import { productKeys, type Product } from '@/api/products'
import { SearchBox } from '@/components/search-box'
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Badge } from '@/components/ui/badge'
import { Button, buttonVariants } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { NativeSelect } from '@/components/ui/native-select'
import {
  adminProductsQuery,
  deleteProduct,
  restoreProduct,
} from '@/features/admin/admin-products-api'
import {
  ADMIN_SORT_LABELS,
  ADMIN_SORTS,
  parseAdminParams,
  toAdminSearchParams,
  type AdminProductParams,
  type AdminSort,
} from '@/features/admin/admin-products-params'
import { apiErrorMessage } from '@/lib/error-message'
import { formatMoney } from '@/lib/money'

/** Starts the list request before the page renders; the page shows loading and error states itself. */
export function adminProductsLoader(queryClient: QueryClient) {
  return ({ request }: LoaderFunctionArgs) => {
    const params = parseAdminParams(new URL(request.url).searchParams)
    queryClient.query(adminProductsQuery(params)).catch(() => undefined)
    return null
  }
}

/** Type guard: true when the text is one of the allowed sort orders. */
function isSort(value: string): value is AdminSort {
  return ADMIN_SORTS.some((sort) => sort === value)
}

/** Confirmation dialog for the soft delete (EP-26). */
function DeleteProductButton({
  product,
  onDone,
  onError,
}: {
  product: Product
  onDone: () => void
  onError: (cause: unknown) => void
}) {
  const [open, setOpen] = useState(false)
  const mutation = useMutation({
    mutationFn: () => deleteProduct(product.id),
    onSuccess: () => {
      setOpen(false)
      onDone()
    },
    onError: (cause) => {
      setOpen(false)
      onError(cause)
    },
  })
  return (
    <AlertDialog open={open} onOpenChange={setOpen}>
      <AlertDialogTrigger asChild>
        <Button variant="outline" size="sm" aria-label={`Delete ${product.name}`}>
          Delete
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogTitle>Delete {product.name}?</AlertDialogTitle>
        <AlertDialogDescription>
          Customers will no longer see it. Existing orders keep their lines, and you can restore the
          product later.
        </AlertDialogDescription>
        <div className="flex flex-wrap justify-end gap-2">
          <AlertDialogCancel asChild>
            <Button type="button" variant="outline" disabled={mutation.isPending}>
              Keep product
            </Button>
          </AlertDialogCancel>
          <Button
            type="button"
            variant="destructive"
            disabled={mutation.isPending}
            aria-busy={mutation.isPending}
            onClick={() => {
              mutation.mutate()
            }}
          >
            {mutation.isPending ? 'Deleting…' : 'Delete product'}
          </Button>
        </div>
      </AlertDialogContent>
    </AlertDialog>
  )
}

/** One row's restore button (EP-27). */
function RestoreProductButton({
  product,
  onDone,
  onError,
}: {
  product: Product
  onDone: () => void
  onError: (cause: unknown) => void
}) {
  const mutation = useMutation({
    mutationFn: () => restoreProduct(product.id),
    onSuccess: onDone,
    onError,
  })
  return (
    <Button
      variant="outline"
      size="sm"
      disabled={mutation.isPending}
      aria-busy={mutation.isPending}
      aria-label={`Restore ${product.name}`}
      onClick={() => {
        mutation.mutate()
      }}
    >
      {mutation.isPending ? 'Restoring…' : 'Restore'}
    </Button>
  )
}

/**
 * The admin product list. Search, sort, "include deleted" and the page are in the URL and sent to the API.
 * Deleted rows are marked "Deleted" and offer Restore instead of Edit and Delete.
 */
export function AdminProductsRoute() {
  const queryClient = useQueryClient()
  const [searchParams, setSearchParams] = useSearchParams()
  const params = parseAdminParams(searchParams)
  const { data, error, isPending, isError, refetch, isFetching } = useQuery(
    adminProductsQuery(params),
  )
  const [actionError, setActionError] = useState<unknown>()
  const [status, setStatus] = useState('')

  function update(change: (current: AdminProductParams) => AdminProductParams, replace = false) {
    setSearchParams((previous) => toAdminSearchParams(change(parseAdminParams(previous))), {
      replace,
    })
  }

  function changed(message: string) {
    setActionError(undefined)
    setStatus(message)
    void queryClient.invalidateQueries({ queryKey: productKeys.all })
  }

  function failed(cause: unknown) {
    setStatus('')
    setActionError(cause)
  }

  const totalPages = Math.max(data?.totalPages ?? 1, 1)
  const pageLink = (page: number) => {
    const query = toAdminSearchParams({ ...params, page }).toString()
    return query === '' ? '/admin/products' : `/admin/products?${query}`
  }

  return (
    <section className="grid gap-4">
      <title>Products · Back office · Orders Shop</title>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
          Products
        </h1>
        <Link to="/admin/products/new" className={buttonVariants()}>
          New product
        </Link>
      </div>

      <div className="flex flex-wrap items-end gap-4">
        <div className="min-w-56 flex-1">
          <SearchBox
            value={params.search ?? ''}
            onSearch={(search) => {
              update((current) => ({ ...current, search, page: 1 }), true)
            }}
          />
        </div>
        <div className="grid w-48 gap-1.5">
          <Label htmlFor="admin-sort">Sort</Label>
          <NativeSelect
            id="admin-sort"
            value={params.sort}
            onChange={(event) => {
              const sort = event.target.value
              if (isSort(sort)) update((current) => ({ ...current, sort, page: 1 }))
            }}
          >
            {ADMIN_SORTS.map((sort) => (
              <option key={sort} value={sort}>
                {ADMIN_SORT_LABELS[sort]}
              </option>
            ))}
          </NativeSelect>
        </div>
        <label className="flex items-center gap-2 pb-2 text-sm">
          <input
            type="checkbox"
            checked={params.includeDeleted}
            onChange={(event) => {
              const includeDeleted = event.target.checked
              update((current) => ({ ...current, includeDeleted, page: 1 }))
            }}
          />
          Include deleted
        </label>
      </div>

      {/* Always in the page, so counts and results of delete and restore are announced. */}
      <p role="status" className="min-h-5 text-sm text-muted-foreground">
        {status !== ''
          ? status
          : data
            ? `${String(data.totalCount)} ${data.totalCount === 1 ? 'product' : 'products'}`
            : ''}
      </p>
      {actionError !== undefined && (
        <p role="alert" className="rounded-md border border-destructive p-3 text-sm">
          {apiErrorMessage(actionError)}
        </p>
      )}

      {isPending && <p role="status">Loading products…</p>}
      {isError && (
        <div
          role="alert"
          className="grid justify-items-start gap-3 rounded-md border border-destructive p-4"
        >
          <p>{apiErrorMessage(error)}</p>
          <Button
            variant="outline"
            disabled={isFetching}
            onClick={() => {
              void refetch()
            }}
          >
            {isFetching ? 'Trying again…' : 'Try again'}
          </Button>
        </div>
      )}

      {data && data.items.length === 0 && (
        <p>{params.page > 1 ? 'No products on this page.' : 'No products found.'}</p>
      )}

      {data && data.items.length > 0 && (
        <div className="overflow-x-auto">
          <table aria-busy={isFetching} className="w-full text-left text-sm">
            <caption className="sr-only">Products</caption>
            <thead>
              <tr className="border-b">
                <th scope="col" className="p-2">
                  Name
                </th>
                <th scope="col" className="p-2">
                  SKU
                </th>
                <th scope="col" className="p-2 text-right">
                  Price
                </th>
                <th scope="col" className="p-2 text-right">
                  Stock
                </th>
                <th scope="col" className="p-2">
                  Status
                </th>
                <th scope="col" className="p-2">
                  Actions
                </th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((product) => (
                <tr key={product.id} className="border-b">
                  <th scope="row" className="p-2 font-medium">
                    {product.name}
                  </th>
                  <td className="p-2">{product.sku}</td>
                  <td className="p-2 text-right">{formatMoney(product.price)}</td>
                  <td className="p-2 text-right">{product.stockQuantity}</td>
                  <td className="p-2">
                    {product.isDeleted === true ? (
                      <Badge variant="muted">Deleted</Badge>
                    ) : (
                      <Badge>Active</Badge>
                    )}
                  </td>
                  <td className="p-2">
                    <div className="flex gap-2">
                      {product.isDeleted === true ? (
                        <RestoreProductButton
                          product={product}
                          onDone={() => {
                            changed(`Restored ${product.name}.`)
                          }}
                          onError={failed}
                        />
                      ) : (
                        <>
                          <Link
                            to={`/admin/products/${product.id}`}
                            aria-label={`Edit ${product.name}`}
                            className={buttonVariants({ variant: 'outline', size: 'sm' })}
                          >
                            Edit
                          </Link>
                          <DeleteProductButton
                            product={product}
                            onDone={() => {
                              changed(`Deleted ${product.name}.`)
                            }}
                            onError={failed}
                          />
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {data && data.totalCount > 0 && (
        <nav aria-label="Pagination" className="flex items-center gap-3">
          {params.page > 1 ? (
            <Link to={pageLink(params.page - 1)} className={buttonVariants({ variant: 'outline' })}>
              Previous
            </Link>
          ) : null}
          <span className="text-sm">
            Page {params.page} of {totalPages}
          </span>
          {params.page < totalPages ? (
            <Link to={pageLink(params.page + 1)} className={buttonVariants({ variant: 'outline' })}>
              Next
            </Link>
          ) : null}
        </nav>
      )}
    </section>
  )
}
