import { useQuery, type QueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useLocation, useSearchParams, type LoaderFunctionArgs } from 'react-router'
import { NativeSelect } from '@/components/ui/native-select'
import { Button, buttonVariants } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import type { Product } from '@/api/products'
import { productsQuery } from '@/features/catalog/catalog-api'
import { apiErrorMessage } from '@/lib/error-message'
import { CatalogFilters } from '@/features/catalog/catalog-filters'
import {
  filterCount,
  hasFilters,
  parseParams,
  SORT_LABELS,
  SORTS,
  toSearchParams,
  type CatalogParams,
  type CatalogSort,
} from '@/features/catalog/catalog-params'
import { CatalogSearch } from '@/features/catalog/catalog-search'
import { ProductCard } from '@/features/catalog/product-card'
import { addMessage, useCart } from '@/hooks/use-cart'

// Starts the request before the page renders. Errors are swallowed here, so the page itself
// shows loading and error states with a retry (CU-1 states).
export function catalogLoader(queryClient: QueryClient) {
  return ({ request }: LoaderFunctionArgs) => {
    const params = parseParams(new URL(request.url).searchParams)
    // Not awaited: the page renders its loading state while the request runs.
    queryClient.query(productsQuery(params)).catch(() => undefined)
    return null
  }
}

function isSort(value: string): value is CatalogSort {
  return SORTS.some((sort) => sort === value)
}

interface Notice {
  // Counts up with every notice, so the same text twice is a new element and is announced again.
  id: number
  kind: 'status' | 'alert'
  text: string
}

// CU-1. Search, filters, sort and page are in the URL and are sent to the API.
export function CatalogRoute() {
  const [searchParams, setSearchParams] = useSearchParams()
  const location = useLocation()
  const params = parseParams(searchParams)
  const { data, error, isPending, isError, refetch, isFetching } = useQuery(productsQuery(params))
  const cart = useCart()
  const [notice, setNotice] = useState<Notice | undefined>()

  // Reads the current URL when it runs, so a debounced search never overwrites a newer change.
  function update(change: (current: CatalogParams) => CatalogParams, replace = false) {
    setSearchParams((previous) => toSearchParams(change(parseParams(previous))), { replace })
  }

  function clearFilters() {
    update((current) => ({
      ...current,
      search: undefined,
      minPrice: undefined,
      maxPrice: undefined,
      inStock: false,
      page: 1,
    }))
  }

  function addToCart(product: Product) {
    const result = cart.add(product)
    const count = cart.lines.some((line) => line.productId === product.id)
      ? cart.count
      : cart.count + 1
    setNotice({
      id: (notice?.id ?? 0) + 1,
      kind: result === 'added' ? 'status' : 'alert',
      text: addMessage(result, product.name, count),
    })
  }

  const filtersActive = hasFilters(params)
  const totalPages = Math.max(data?.totalPages ?? 1, 1)
  const pageLink = (page: number) => {
    const query = toSearchParams({ ...params, page }).toString()
    return query === '' ? '/products' : `/products?${query}`
  }

  return (
    <section className="grid gap-4">
      <title>Products · Orders Shop</title>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
          Products
        </h1>
        {/* Always in the page: a new count is announced when search, filters or paging change the list. */}
        <p role="status" className="min-h-5 text-sm text-muted-foreground">
          {data
            ? `${String(data.totalCount)} ${data.totalCount === 1 ? 'product' : 'products'}`
            : ''}
        </p>
      </div>

      <div className="flex flex-wrap items-start gap-4">
        <div className="min-w-56 flex-1">
          <CatalogSearch
            value={params.search ?? ''}
            onSearch={(search) => {
              update((current) => ({ ...current, search, page: 1 }), true)
            }}
          />
        </div>
        <div className="grid w-48 gap-1.5">
          <Label htmlFor="catalog-sort">Sort</Label>
          <NativeSelect
            id="catalog-sort"
            value={params.sort}
            onChange={(event) => {
              const sort = event.target.value
              if (isSort(sort)) update((current) => ({ ...current, sort, page: 1 }))
            }}
          >
            {SORTS.map((sort) => (
              <option key={sort} value={sort}>
                {SORT_LABELS[sort]}
              </option>
            ))}
          </NativeSelect>
        </div>
      </div>

      <CatalogFilters
        key={`filters-${String(params.minPrice)}-${String(params.maxPrice)}-${String(params.inStock)}`}
        params={params}
        canClear={filtersActive}
        onApply={(filters) => {
          update((current) => ({ ...current, ...filters, page: 1 }))
        }}
        onClear={clearFilters}
      />
      {filtersActive && (
        <p className="text-sm text-muted-foreground">
          {filterCount(params)} {filterCount(params) === 1 ? 'filter' : 'filters'} applied
        </p>
      )}

      {/* Always in the page, so cart changes are announced. */}
      <p role="status" className="min-h-5 text-sm">
        {notice?.kind === 'status' ? <span key={notice.id}>{notice.text}</span> : null}
      </p>
      {notice?.kind === 'alert' && (
        <p role="alert" className="rounded-md border border-destructive p-3 text-sm">
          {notice.text}
        </p>
      )}

      {isPending && (
        <>
          <p role="status" className="sr-only">
            Loading products…
          </p>
          <ul aria-hidden="true" className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {Array.from({ length: 8 }, (_, index) => (
              <li key={index} className="h-40 animate-pulse rounded-lg border bg-muted" />
            ))}
          </ul>
        </>
      )}

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
        <div className="grid justify-items-start gap-3">
          {params.page > 1 ? (
            <>
              <p>No products on this page.</p>
              <Link to={pageLink(1)} className={buttonVariants({ variant: 'outline' })}>
                Go to page 1
              </Link>
            </>
          ) : filtersActive ? (
            <>
              <p>No products match your search.</p>
              <Button type="button" variant="outline" onClick={clearFilters}>
                Clear filters
              </Button>
            </>
          ) : (
            <p>No products yet. Check back later.</p>
          )}
        </div>
      )}

      {data && data.items.length > 0 && (
        <ul aria-busy={isFetching} className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {data.items.map((product) => (
            <ProductCard
              key={product.id}
              product={product}
              inCart={cart.lines.find((line) => line.productId === product.id)?.quantity ?? 0}
              listSearch={location.search}
              onAdd={addToCart}
            />
          ))}
        </ul>
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
