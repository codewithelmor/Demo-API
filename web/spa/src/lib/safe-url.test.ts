import { describe, expect, it } from 'vitest'
import { returnTargetFor } from '@/lib/session'
import { safeHttpUrl, safeReturnUrl } from '@/lib/safe-url'

describe('safeHttpUrl', () => {
  it('accepts http and https only', () => {
    expect(safeHttpUrl('https://example.test/a')).toBe('https://example.test/a')
    expect(safeHttpUrl('http://example.test/')).toBe('http://example.test/')
    // Built from parts so the no-script-url lint rule does not flag the test data.
    expect(safeHttpUrl(['java', 'script:alert(1)'].join(''))).toBeUndefined()
    expect(safeHttpUrl('data:text/html,x')).toBeUndefined()
    expect(safeHttpUrl('/relative')).toBeUndefined()
    expect(safeHttpUrl(null)).toBeUndefined()
  })
})

describe('safeReturnUrl', () => {
  it('accepts same-origin paths', () => {
    expect(safeReturnUrl('/products?page=2')).toBe('/products?page=2')
    expect(safeReturnUrl('/orders/abc#top')).toBe('/orders/abc#top')
    expect(safeReturnUrl('/apiary')).toBe('/apiary')
  })

  it('rejects anything that could leave the site', () => {
    const unsafe = ['//evil.test', '/\\evil.test', 'https://evil.test', 'products', '', '/a\nb']
    for (const value of unsafe) {
      expect(safeReturnUrl(value)).toBeUndefined()
    }
    expect(safeReturnUrl(null)).toBeUndefined()
  })
})

describe('returnTargetFor', () => {
  const customer = {
    email: 'a@example.test',
    emailConfirmed: true,
    roles: ['Customer'],
    customerId: 'c',
  }
  const admin = {
    email: 'b@example.test',
    emailConfirmed: true,
    roles: ['Admin'],
    customerId: null,
  }

  it('uses the return path when the role may open it', () => {
    expect(returnTargetFor(customer, '/orders?page=2')).toBe('/orders?page=2')
    expect(returnTargetFor(admin, '/admin/users')).toBe('/admin/users')
  })

  it('falls back to the role home', () => {
    expect(returnTargetFor(customer, undefined)).toBe('/products')
    expect(returnTargetFor(customer, '/admin')).toBe('/products')
    expect(returnTargetFor(customer, '/admin/users?x=1')).toBe('/products')
    expect(returnTargetFor(admin, '/orders')).toBe('/admin')
    expect(returnTargetFor(admin, undefined)).toBe('/admin')
  })
})
