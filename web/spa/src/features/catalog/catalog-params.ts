import { z } from '@/lib/zod'

/**
 * The catalog's filters, sort and page live in the URL (information architecture CU-1):
 * `/products?search&minPrice&maxPrice&inStock&sort&page`. That makes every filtered view a link that can be
 * bookmarked, shared, and used with the Back button.
 * Everything is validated here. Bad values fall back to the default, so a hand-edited URL never breaks the page.
 */

/** Products per page. API maximum is 100 (catalog 1.4). */
export const PAGE_SIZE = 20
/** Shortest search text that is sent to the API. */
export const SEARCH_MIN = 2
/** Longest search text. */
export const SEARCH_MAX = 100

/** The sort orders the API accepts. A leading `-` means descending. */
export const SORTS = ['name', '-name', 'price', '-price'] as const
/** One of {@link SORTS}. */
export type CatalogSort = (typeof SORTS)[number]

/** Text for each sort order in the dropdown. */
export const SORT_LABELS: Record<CatalogSort, string> = {
  name: 'Name A-Z',
  '-name': 'Name Z-A',
  price: 'Price low to high',
  '-price': 'Price high to low',
}

/** A price is 0 or more with at most 2 decimals (money rule, catalog 1.3). */
export const PRICE_PATTERN = /^\d+(\.\d{1,2})?$/

/** A price from the URL: a valid number, or `undefined` if the text is not a valid price. */
const price = z.string().regex(PRICE_PATTERN).transform(Number).optional().catch(undefined)

/** What the URL's query string may contain. `.catch(...)` supplies the fallback for an invalid value. */
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

/** The catalog view the URL describes: filters, sort and page. */
export type CatalogParams = z.infer<typeof paramsSchema>

/** Reads the catalog view from the URL's query string. Never throws; invalid parts fall back to defaults. */
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

/** Writes a catalog view back into a query string. Only values that differ from the defaults go into the URL. */
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

/** True when any filter (search, price range or in-stock) is active. */
export function hasFilters(params: CatalogParams): boolean {
  return (
    params.search !== undefined ||
    params.minPrice !== undefined ||
    params.maxPrice !== undefined ||
    params.inStock
  )
}

/** How many filters are active (for the "3 filters" style text). */
export function filterCount(params: CatalogParams): number {
  return (
    Number(params.search !== undefined) +
    Number(params.minPrice !== undefined) +
    Number(params.maxPrice !== undefined) +
    Number(params.inStock)
  )
}
