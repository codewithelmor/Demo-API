import { z } from '@/lib/zod'

/** EP-24/25 rules (endpoint catalog): name 1-200, SKU 1-50 letters, digits and hyphens, description up to 2000, price 0.01-9,999,999,999.99, stock 0-1,000,000. The server stays the authority. */
export const productSchema = z.object({
  name: z.string().trim().min(1, 'Enter a name.').max(200, 'Use 200 characters or fewer.'),
  sku: z
    .string()
    .trim()
    .min(1, 'Enter a SKU.')
    .max(50, 'Use 50 characters or fewer.')
    .regex(/^[A-Za-z0-9-]+$/, 'Use letters, digits and hyphens only.'),
  description: z.string().trim().max(2000, 'Use 2000 characters or fewer.'),
  // The inputs hold text; the schema turns it into numbers.
  price: z
    .string()
    .trim()
    .regex(/^\d+(\.\d{1,2})?$/, 'Enter a price with at most 2 decimals, for example 12.50.')
    .transform(Number)
    .refine((value) => value >= 0.01 && value <= 9_999_999_999.99, {
      error: 'Enter a price from 0.01 to 9999999999.99.',
    }),
  stockQuantity: z
    .string()
    .trim()
    .regex(/^\d+$/, 'Enter a whole number, 0 or more.')
    .transform(Number)
    .refine((value) => value <= 1_000_000, { error: 'Use 1,000,000 or fewer.' }),
})

/** What the user types (text for the number fields). */
export type ProductFormInput = z.input<typeof productSchema>
/** What the form submits after validation. */
export type ProductFormValues = z.output<typeof productSchema>
