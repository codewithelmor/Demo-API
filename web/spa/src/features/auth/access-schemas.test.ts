import { describe, expect, it } from 'vitest'
import {
  codePointLength,
  emailSchema,
  registerSchema,
  resetPasswordSchema,
} from '@/features/auth/access-schemas'

const valid = {
  firstName: 'Sample',
  lastName: 'Person',
  email: 'sample.person@example.test',
  password: 'correct horse battery staple',
  phone: '',
}

describe('access schemas (endpoint catalog 1.5)', () => {
  it('counts the password in Unicode code points, not UTF-16 units', () => {
    // 15 emoji are 15 code points but 30 UTF-16 units.
    const emoji = '😀'.repeat(15)
    expect(emoji.length).toBe(30)
    expect(codePointLength(emoji)).toBe(15)
    expect(resetPasswordSchema.safeParse({ newPassword: emoji }).success).toBe(true)
    expect(resetPasswordSchema.safeParse({ newPassword: '😀'.repeat(14) }).success).toBe(false)
  })

  it('accepts 15 to 128 code points and no composition rules', () => {
    expect(registerSchema.safeParse({ ...valid, password: 'a'.repeat(15) }).success).toBe(true)
    expect(registerSchema.safeParse({ ...valid, password: 'a'.repeat(128) }).success).toBe(true)
    expect(registerSchema.safeParse({ ...valid, password: 'a'.repeat(14) }).success).toBe(false)
    expect(registerSchema.safeParse({ ...valid, password: 'a'.repeat(129) }).success).toBe(false)
    // Spaces and all Unicode are allowed.
    expect(registerSchema.safeParse({ ...valid, password: 'one two three four' }).success).toBe(
      true,
    )
  })

  it('requires names of 1 to 100 characters after trimming', () => {
    expect(registerSchema.safeParse({ ...valid, firstName: '   ' }).success).toBe(false)
    expect(registerSchema.safeParse({ ...valid, lastName: 'x'.repeat(101) }).success).toBe(false)
    expect(registerSchema.safeParse({ ...valid, lastName: 'x'.repeat(100) }).success).toBe(true)
  })

  it('accepts an empty phone or E.164 and rejects the rest', () => {
    expect(registerSchema.safeParse({ ...valid, phone: '' }).success).toBe(true)
    expect(registerSchema.safeParse({ ...valid, phone: '+14155550123' }).success).toBe(true)
    for (const phone of ['4155550123', '+0415555012', '+1 415 555 0123', '+1234567890123456']) {
      expect(registerSchema.safeParse({ ...valid, phone }).success, phone).toBe(false)
    }
  })

  it('requires a valid email of at most 256 characters', () => {
    expect(emailSchema.safeParse({ email: 'not-an-email' }).success).toBe(false)
    expect(emailSchema.safeParse({ email: '' }).success).toBe(false)
    expect(emailSchema.safeParse({ email: `${'a'.repeat(250)}@x.test` }).success).toBe(false)
    expect(emailSchema.parse({ email: '  sample.person@example.test ' }).email).toBe(
      'sample.person@example.test',
    )
  })
})
