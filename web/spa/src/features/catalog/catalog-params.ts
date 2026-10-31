import { z } from '@/lib/zod'

// The catalog's filters, sort and page live in the URL (information architecture CU-1):
//   /products?search&minPrice&maxPrice&inStock&sort&page
// Everything is validated here. Bad values fall back to the default, so a hand-edited URL never breaks the page.

export const PAGE_SIZE = 20 // API maximum is 100 (catalog 1.4)
export const SEARCH_MIN = 2
export const SEARCH_MAX = 100

export const SORTS = ['name', '-name', 'price', '-price'] as const
export type CatalogSort = (typeof SORTS)[number]

export const SORT_LABELS: Record<CatalogSort, string> = {
  name: 'Name A-Z',
  '-name': 'Name Z-A',
  price: 'Price low to high',
  '-price': 'Price high to low',
}

// A price is 0 or more with at most 2 decimals (money rule, catalog 1.3).
export const PRICE_PATTERN = /^\d+(\.\d{1,2})?$/

const price = z.string().regex(PRICE_PATTERN).transform(Number).optional().catch(undefined)

const paramsSchema = z.object({
  // Fewer than 2 characters is never sent to the API.
  search: z.string().trim().min(SEARCH_MIN).max(SEARCH_MAX).optional().catch(undefined),
  minPrice: price,
  maxPrice: price,
  inStock: z
    .string()
    .optional()
    .transform((value) => value === 'true'),
  sort: z.enum(SORTS).catch('name'),
  page: z.coerce.number().int().min(1).catch(1),
})

export type CatalogParams = z.infer<typeof paramsSchema>

export function parseParams(searchParams: URLSearchParams): CatalogParams {
  const parsed = paramsSchema.parse(Object.fromEntries(searchParams))
  // min above max would be a 400 from the API; the filter form never produces it.
  if (
    parsed.minPrice !== undefined &&
    parsed.maxPrice !== undefined &&
    parsed.minPrice > parsed.maxPrice
  ) {
    return { ...parsed, maxPrice: undefined }
  }
  return parsed
}

// Only values that differ from the defaults go into the URL.
export function toSearchParams(params: CatalogParams): URLSearchParams {
  const result = new URLSearchParams()
  if (params.search) result.set('search', params.search)
  if (params.minPrice !== undefined) result.set('minPrice', String(params.minPrice))
  if (params.maxPrice !== undefined) result.set('maxPrice', String(params.maxPrice))
  if (params.inStock) result.set('inStock', 'true')
  if (params.sort !== 'name') result.set('sort', params.sort)
  if (params.page > 1) result.set('page', String(params.page))
  return result
}

export function hasFilters(params: CatalogParams): boolean {
  return (
    params.search !== undefined ||
    params.minPrice !== undefined ||
    params.maxPrice !== undefined ||
    params.inStock
  )
}

export function filterCount(params: CatalogParams): number {
  return (
    Number(params.search !== undefined) +
    Number(params.minPrice !== undefined) +
    Number(params.maxPrice !== undefined) +
    Number(params.inStock)
  )
}
