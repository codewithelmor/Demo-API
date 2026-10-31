import { SEARCH_MAX, SEARCH_MIN } from '@/lib/field-rules'
import { z } from '@/lib/zod'

/**
 * The admin product list keeps its search, sort, "include deleted" switch and page in the URL
 * (`/admin/products?search&sort&includeDeleted&page`), like the catalog. Bad values fall back to defaults.
 */

/** Rows per page. */
export const ADMIN_PAGE_SIZE = 20

/** The sort orders EP-22 accepts. A leading `-` means descending. */
export const ADMIN_SORTS = [
  'name',
  '-name',
  'sku',
  '-sku',
  'price',
  '-price',
  'stockQuantity',
  '-stockQuantity',
  'createdAt',
  '-createdAt',
] as const
/** One of {@link ADMIN_SORTS}. */
export type AdminSort = (typeof ADMIN_SORTS)[number]

/** Text for each sort order in the dropdown. */
export const ADMIN_SORT_LABELS: Record<AdminSort, string> = {
  name: 'Name A-Z',
  '-name': 'Name Z-A',
  sku: 'SKU A-Z',
  '-sku': 'SKU Z-A',
  price: 'Price low to high',
  '-price': 'Price high to low',
  stockQuantity: 'Stock low to high',
  '-stockQuantity': 'Stock high to low',
  createdAt: 'Oldest first',
  '-createdAt': 'Newest first',
}

const paramsSchema = z.object({
  search: z.string().trim().min(SEARCH_MIN).max(SEARCH_MAX).optional().catch(undefined),
  includeDeleted: z
    .string()
    .optional()
    .transform((value) => value === 'true'),
  sort: z.enum(ADMIN_SORTS).catch('name'),
  page: z.coerce.number().int().min(1).catch(1),
})

/** The admin list view the URL describes. */
export type AdminProductParams = z.infer<typeof paramsSchema>

/** Reads the view from the URL's query string. Never throws. */
export function parseAdminParams(searchParams: URLSearchParams): AdminProductParams {
  return paramsSchema.parse(Object.fromEntries(searchParams))
}

/** Writes a view back into a query string. Only non-default values go into the URL. */
export function toAdminSearchParams(params: AdminProductParams): URLSearchParams {
  const result = new URLSearchParams()
  if (params.search) result.set('search', params.search)
  if (params.includeDeleted) result.set('includeDeleted', 'true')
  if (params.sort !== 'name') result.set('sort', params.sort)
  if (params.page > 1) result.set('page', String(params.page))
  return result
}
