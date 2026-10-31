import { z } from '@/lib/zod'

// Mirrors the API's LoginRequest limits (email 256, password 1024). The server stays the authority.
export const signInSchema = z.object({
  email: z
    .string()
    .trim()
    .max(256, 'Email is too long.')
    .pipe(z.email('Enter a valid email address.')),
  password: z.string().min(1, 'Enter your password.').max(1024, 'Password is too long.'),
})

export type SignInValues = z.infer<typeof signInSchema>
