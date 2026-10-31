import { api, expectOk } from '@/api/client'
import type { RegisterValues } from '@/features/auth/access-schemas'

/**
 * Calls for the account endpoints a visitor who is not signed in may use (EP-01, 02, 03, 10, 11), through
 * the typed client. The BFF forwards them without a token. Mutations rely on the client's 15 s timeout;
 * the confirm loader passes its own signal.
 *
 * "EP-xx" numbers refer to the endpoint catalog in docs/product/04-architecture/endpoint-catalog.md.
 */

/** Creates an account. */
// EP-01. Always 202 with the same text, so the response body is not used.
export function registerAccount(values: RegisterValues): Promise<void> {
  const { phone, ...rest } = values
  return expectOk(
    api.POST('/api/v1/auth/register', { body: phone === '' ? rest : { ...rest, phone } }),
  )
}

/** Confirms an email address with the `userId` and `token` from the email link. */
// EP-02. 204, also when already confirmed; 400 invalid-token for anything else.
export function confirmEmail(
  body: { userId: string; token: string },
  signal?: AbortSignal,
): Promise<void> {
  return expectOk(api.POST('/api/v1/auth/confirm-email', { body, ...(signal && { signal }) }))
}

/** Asks for a new confirmation email. */
// EP-03. Always 202.
export function resendConfirmation(email: string): Promise<void> {
  return expectOk(api.POST('/api/v1/auth/resend-confirmation', { body: { email } }))
}

/** Asks for a password-reset email. */
// EP-10. Always 202.
export function forgotPassword(email: string): Promise<void> {
  return expectOk(api.POST('/api/v1/auth/forgot-password', { body: { email } }))
}

/** Sets a new password using the token from the reset email. */
// EP-11. 204; 400 invalid-token or validation-failed.
export function resetPassword(body: {
  email: string
  token: string
  newPassword: string
}): Promise<void> {
  return expectOk(api.POST('/api/v1/auth/reset-password', { body }))
}
