import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'
import { recordCsp, requireEnvForFile } from './support'

// Place order, My orders, order detail and cancel (US-F029..F032) against the compose stack.
// These need a confirmed Customer account that exists in the stack and at least one product in stock.
// Set E2E_CUSTOMER_EMAIL and E2E_CUSTOMER_PASSWORD (generated test data, never a real customer).
const email = process.env.E2E_CUSTOMER_EMAIL
const password = process.env.E2E_CUSTOMER_PASSWORD

requireEnvForFile('E2E_CUSTOMER_EMAIL', 'E2E_CUSTOMER_PASSWORD')
// One shared account and a stack with state: the tests of a file run one after the other.
test.describe.configure({ mode: 'serial' })

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await page.getByLabel('Email').fill(email ?? '')
  await page.getByLabel('Password', { exact: true }).fill(password ?? '')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page.getByRole('heading', { level: 1, name: 'Products' })).toBeVisible()
}

async function expectNoAxeViolations(page: Page) {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag22aa'])
    .analyze()
  expect(results.violations).toEqual([])
}

async function addFirstProduct(page: Page) {
  await page
    .getByRole('button', { name: /^Add to cart:/ })
    .first()
    .click()
  await expect(page.getByRole('link', { name: /^Cart, 1 product$/ })).toBeVisible()
}

// Orders created by the current test, cancelled afterwards: a Pending order counts toward the limit of open orders.
const created: string[] = []
test.afterEach(async ({ page }) => {
  while (created.length > 0) {
    const id = created.pop()
    // The session cookie is in the page's context. The BFF needs X-CSRF and an allowed Origin on every unsafe call
    // (page.request does not send Origin by itself; without it this cleanup got 403 and orders stayed Pending).
    const response = await page.request.post(`/api/v1/customers/me/orders/${id ?? ''}/cancel`, {
      headers: { 'X-CSRF': '1', Origin: new URL(page.url()).origin },
      data: {},
    })
    // 204 cancelled; 409 already cancelled (the test did it). Anything else would leak a Pending order.
    expect([204, 409]).toContain(response.status())
  }
})

// Adds the first product on the list to the cart and places the order. Ends on the order page.
async function placeOneOrder(page: Page) {
  await addFirstProduct(page)
  await page.goto('/cart')
  await expectNoAxeViolations(page)
  await page.getByRole('button', { name: 'Place order' }).click()
  await expect(page.getByRole('heading', { level: 1, name: /^Order #/ })).toBeVisible()
  const id = /[/]orders[/]([0-9a-f-]{36})/.exec(page.url())?.[1]
  if (id) created.push(id)
}

test.describe('place order', () => {
  test('[US-F029][AC1] places an order, shows it, and empties the cart', async ({ page }) => {
    await signIn(page)
    await placeOneOrder(page)

    await expect(page.getByText('Order placed. We will confirm it soon.')).toBeVisible()
    await expect(page.getByText('Pending', { exact: true }).first()).toBeVisible()
    await expectNoAxeViolations(page)
    await expect(page.getByRole('link', { name: 'Cart, 0 products' })).toBeVisible()
  })

  test('[US-F029][AC5] a double click on Place order sends one request', async ({ page }) => {
    await signIn(page)
    await addFirstProduct(page)
    await page.goto('/cart')
    let posts = 0
    page.on('request', (request) => {
      if (request.method() === 'POST' && request.url().endsWith('/api/v1/customers/me/orders')) {
        posts++
      }
    })

    await page.getByRole('button', { name: 'Place order' }).dblclick()
    await expect(page.getByRole('heading', { level: 1, name: /^Order #/ })).toBeVisible()
    const id = /[/]orders[/]([0-9a-f-]{36})/.exec(page.url())?.[1]
    if (id) created.push(id) // cancelled afterwards, or it stays Pending and holds the stock

    expect(posts).toBe(1)
  })
})

test.describe('my orders', () => {
  test(
    '[US-F030][AC1] lists orders and keeps the status filter in the URL',
    { tag: '@cross-browser' },
    async ({ page }) => {
      await signIn(page)
      await page.getByRole('link', { name: 'My orders' }).click()
      await expectNoAxeViolations(page)

      await page.getByLabel('Pending').check()
      await page.getByRole('button', { name: 'Apply filters' }).click()

      await expect(page).toHaveURL(/status=Pending/)
    },
  )

  test('[US-F030][AC2] To before From shows an error and sends no request', async ({ page }) => {
    await signIn(page)
    await page.goto('/orders')
    // Let the first list load finish; only requests caused by Apply filters count.
    await expect(page.getByRole('status').filter({ hasText: /orders?$/ })).toBeVisible()
    const requests: string[] = []
    page.on('request', (request) => {
      if (request.url().includes('/customers/me/orders?')) requests.push(request.url())
    })

    await page.getByLabel('From').fill('2026-03-10')
    await page.getByLabel('To').fill('2026-03-01')
    await page.getByRole('button', { name: 'Apply filters' }).click()

    await expect(page.getByText('The To date must be on or after the From date.')).toBeVisible()
    expect(requests).toHaveLength(0)
  })

  test('[US-F030][AC3] a filter with no match shows an empty state with Clear filters', async ({
    page,
  }) => {
    await signIn(page)
    await page.goto('/orders?from=2000-01-01&to=2000-01-02')

    await expect(page.getByText('No orders match these filters.')).toBeVisible()
    await page.getByRole('button', { name: 'Clear filters' }).first().click()
    await expect(page).toHaveURL(/\/orders$/)
  })
})

test.describe('order detail and cancel', () => {
  test('[US-F031][AC1] shows items, total, currency and status history', async ({ page }) => {
    await signIn(page)
    await placeOneOrder(page)

    await expect(page.getByRole('table', { name: 'Items in this order' })).toBeVisible()
    await expect(page.getByText(/^Total .*\(\w{3}\)$/)).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Status history' })).toBeVisible()
  })

  test('[US-F031][AC3] another order ID shows Order not found', async ({ page }) => {
    await signIn(page)
    await page.goto('/orders/00000000-0000-4000-8000-000000000000')

    await expect(page.getByRole('heading', { level: 1, name: 'Order not found' })).toBeVisible()
  })

  // Finding L-3: opening a Radix modal must cause no CSP violation (style-src nonce from the BFF, Zod without
  // eval, see src/lib/zod.ts and src/app/csp-nonce.ts).
  test('[L-3] opening and closing the cancel dialog causes no CSP violation', async ({ page }) => {
    const csp = await recordCsp(page)
    await signIn(page)
    await placeOneOrder(page)

    await page.getByRole('button', { name: 'Cancel order' }).click()
    await expect(page.getByRole('alertdialog', { name: 'Cancel this order?' })).toBeVisible()
    await page.keyboard.press('Escape')
    await expect(page.getByRole('alertdialog')).toHaveCount(0)

    expect(csp.violations).toEqual([])
    expect(csp.consoleErrors).toEqual([])
  })

  test('[US-F032][AC1] cancels a Pending order with a reason and shows Cancelled', async ({
    page,
  }) => {
    await signIn(page)
    await placeOneOrder(page)

    await page.getByRole('button', { name: 'Cancel order' }).click()
    const dialog = page.getByRole('alertdialog', { name: 'Cancel this order?' })
    await expectNoAxeViolations(page)
    await dialog.getByLabel('Reason (optional)').fill('Ordered by mistake')
    await dialog.getByRole('button', { name: 'Cancel order' }).click()

    await expect(dialog).toBeHidden()
    await expect(page.getByText('Order cancelled.')).toBeVisible()
    await expect(page.getByText(/Reason: Ordered by mistake/)).toBeVisible()
    await expect(page.getByRole('button', { name: 'Cancel order' })).toHaveCount(0)
  })
})
