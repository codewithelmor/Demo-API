import { z } from '@/lib/zod'

// Field rules shared by the sign-up form and the profile and password forms. They mirror the API rules in
// endpoint-catalog section 1.5 (NFR-F21); the server stays the authority.

export const PASSWORD_MIN = 15
export const PASSWORD_MAX = 128

// Passwords are counted in Unicode code points, like the API (FR-003): iterating a string
// yields code points, so an emoji counts as 1 (value.length would count 2).
export function codePointLength(value: string): number {
  return Array.from(value).length
}

export const name = (label: string) =>
  z
    .string()
    .trim()
    .min(1, `Enter your ${label}.`)
    .max(
      100,
      `${label.charAt(0).toUpperCase()}${label.slice(1)} is too long. Use 100 characters or fewer.`,
    )

// No composition rules, all Unicode allowed, only a length range.
export const password = z
  .string()
  .refine((value) => codePointLength(value) >= PASSWORD_MIN, {
    error: `Use at least ${String(PASSWORD_MIN)} characters. A short sentence works well.`,
  })
  .refine((value) => codePointLength(value) <= PASSWORD_MAX, {
    error: `Use ${String(PASSWORD_MAX)} characters or fewer.`,
  })

export const E164 = /^\+[1-9][0-9]{1,14}$/

// Optional: an empty field is allowed and is not sent.
export const phone = z
  .string()
  .trim()
  .refine((value) => value === '' || E164.test(value), {
    error: 'Enter the phone number with + and the country code, for example +14155550123.',
  })
