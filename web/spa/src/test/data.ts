import type { components } from '@/api/schema'
import type { SessionUser } from '@/lib/session'

// Generated sample data only. Never real customer data in tests.
export const customerUser: SessionUser = {
  email: 'customer.one@example.test',
  emailConfirmed: true,
  roles: ['Customer'],
  customerId: '5b1b7c9e-0000-4000-8000-000000000001',
}

export const adminUser: SessionUser = {
  email: 'admin.one@example.test',
  emailConfirmed: true,
  roles: ['Admin'],
  customerId: null,
}

type Product = components['schemas']['ProductResponse']

export function makeProduct(n: number, overrides: Partial<Product> = {}): Product {
  return {
    id: `00000000-0000-4000-8000-${String(n).padStart(12, '0')}`,
    name: `Sample product ${String(n)}`,
    sku: `SKU-${String(1000 + n)}`,
    description: null,
    price: 10 + n + 0.5,
    stockQuantity: n % 2 === 0 ? 0 : 5,
    createdAt: '2026-01-15T09:30:00Z',
    ...overrides,
  }
}

export const productId = (n: number) => `00000000-0000-4000-8000-${String(n).padStart(12, '0')}`

type MyOrder = components['schemas']['MyOrderResponse']

// A Pending order with one line (2 x $11.50 = $23.00). Generated data only.
export function makeOrder(overrides: Partial<MyOrder> = {}): MyOrder {
  return {
    id: '11111111-0000-4000-8000-000000000001',
    status: 'Pending',
    total: 23,
    currency: 'USD',
    createdAt: '2026-03-10T09:30:00Z',
    lastModifiedAt: '2026-03-10T09:30:00Z',
    items: [{ productId: productId(1), quantity: 2, unitPrice: 11.5, lineTotal: 23 }],
    history: [{ from: null, to: 'Pending', at: '2026-03-10T09:30:00Z', reason: null }],
    ...overrides,
  }
}
