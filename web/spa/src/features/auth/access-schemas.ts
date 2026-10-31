import { z } from '@/lib/zod'
import {
  codePointLength,
  E164,
  name,
  password,
  PASSWORD_MAX,
  PASSWORD_MIN,
  phone,
} from '@/lib/field-rules'

/**
 * Zod schemas (descriptions of valid input) for the anonymous account forms. The rules live in
 * lib/field-rules.ts (shared with the profile forms). The server also applies a password blocklist that
 * the client cannot know.
 */
export { codePointLength, E164, PASSWORD_MAX, PASSWORD_MIN }

/** Email: required, valid address, 256 characters or fewer. Trimmed first, like the API. */
const email = z
  .string()
  .trim()
  .max(256, 'Email is too long. Use 256 characters or fewer.')
  .pipe(z.email('Enter a valid email address.'))

/** The sign-up form. */
export const registerSchema = z.object({
  firstName: name('first name'),
  lastName: name('last name'),
  email,
  password,
  phone,
})
/** The sign-up form values, derived from the schema so the two never drift apart. */
export type RegisterValues = z.infer<typeof registerSchema>

/** A form with only an email (resend confirmation, forgot password). */
export const emailSchema = z.object({ email })
/** Values of the email-only form. */
export type EmailValues = z.infer<typeof emailSchema>

/** EP-11: the email and token come from the link, so the form only holds the new password. */
export const resetPasswordSchema = z.object({ newPassword: password })
/** Values of the reset-password form. */
export type ResetPasswordValues = z.infer<typeof resetPasswordSchema>
