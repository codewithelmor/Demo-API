import { z } from '@/lib/zod'

/**
 * Form field rules shared by the sign-up form and the profile and password forms. Each rule is a Zod
 * schema: a small description of what valid input looks like, used to show errors before anything is sent.
 * They mirror the API rules in endpoint-catalog section 1.5 (NFR-F21); the server stays the authority.
 */

/** Fewest characters a password may have. */
export const PASSWORD_MIN = 15
/** Most characters a password may have. */
export const PASSWORD_MAX = 128

/**
 * Length of a text in Unicode code points, like the API counts passwords (FR-003).
 * Iterating a string yields code points, so an emoji counts as 1 (`value.length` would count 2).
 */
export function codePointLength(value: string): number {
  return Array.from(value).length
}

/**
 * Rule for a required name field, 1 to 100 characters after trimming spaces.
 * @param label Lower-case field name used in the error text, for example `"first name"`.
 */
export const name = (label: string) =>
  z
    .string()
    .trim()
    .min(1, `Enter your ${label}.`)
    .max(
      100,
      `${label.charAt(0).toUpperCase()}${label.slice(1)} is too long. Use 100 characters or fewer.`,
    )

/** Rule for a password: no composition rules (no "needs a digit"), all Unicode allowed, only a length range. */
export const password = z
  .string()
  .refine((value) => codePointLength(value) >= PASSWORD_MIN, {
    error: `Use at least ${String(PASSWORD_MIN)} characters. A short sentence works well.`,
  })
  .refine((value) => codePointLength(value) <= PASSWORD_MAX, {
    error: `Use ${String(PASSWORD_MAX)} characters or fewer.`,
  })

/** Phone number format E.164: `+`, a country code that does not start with 0, up to 15 digits in total. */
export const E164 = /^\+[1-9][0-9]{1,14}$/

/** Rule for an optional phone number: an empty field is allowed and is not sent. */
export const phone = z
  .string()
  .trim()
  .refine((value) => value === '' || E164.test(value), {
    error: 'Enter the phone number with + and the country code, for example +14155550123.',
  })

/** Shortest search text that is sent to the API. */
export const SEARCH_MIN = 2
/** Longest search text. */
export const SEARCH_MAX = 100
