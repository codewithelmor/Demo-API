import { z } from '@/lib/zod'
import { name, password, phone } from '@/lib/field-rules'

/** EP-36: Name 1-100 trimmed, Phone optional E.164 (catalog 1.5). An empty phone clears it. */
export const profileSchema = z.object({
  firstName: name('first name'),
  lastName: name('last name'),
  phone,
})
/** Values of the edit-profile form. */
export type ProfileValues = z.infer<typeof profileSchema>

/**
 * EP-09: the current password is checked by the API only (required, 1024 or fewer); the new one follows
 * the password rule. Spaces and pasted text are kept as typed.
 */
export const changePasswordSchema = z.object({
  currentPassword: z
    .string()
    .min(1, 'Enter your current password.')
    .max(1024, 'Use 1024 characters or fewer.'),
  newPassword: password,
})
/** Values of the change-password form. */
export type ChangePasswordValues = z.infer<typeof changePasswordSchema>

/** EP-37 (CU-10): the current password, and the "I understand" box. */
export const deleteAccountSchema = z.object({
  currentPassword: z
    .string()
    .min(1, 'Enter your current password.')
    .max(1024, 'Use 1024 characters or fewer.'),
  understood: z.boolean().refine((value) => value, { error: 'Tick the box to continue.' }),
})
/** Values of the delete-account form. */
export type DeleteAccountValues = z.infer<typeof deleteAccountSchema>
