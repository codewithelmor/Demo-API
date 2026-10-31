import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'
import { requireEnvForFile } from './support'

// Catalog, product detail and cart (US-F026..F028) against the compose stack.
// These need a confirmed Customer account that exists in the stack. Set E2E_CUSTOMER_EMAIL and
// E2E_CUSTOMER_PASSWORD (generated test data, never a real customer). Without them the tests skip.
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

test.describe('catalog', () => {
  test(
    '[US-F026][AC1] a search of 2 or more characters filters the list and is in the URL',
    { tag: '@cross-browser' },
    async ({ page }) => {
      await signIn(page)
      await expectNoAxeViolations(page)

      const search = page.getByLabel('Search name or SKU')
      await search.fill('ab')

      await expect(page).toHaveURL(/search=ab/)
      await expect(page.getByText(/\d+ products?/)).toBeVisible()
    },
  )

  test('[US-F026][AC2] one character sends no search and shows a hint', async ({ page }) => {
    await signIn(page)
    const searches: string[] = []
    page.on('request', (request) => {
      if (request.url().includes('/api/v1/products?') && request.url().includes('search=')) {
        searches.push(request.url())
      }
    })

    await page.getByLabel('Search name or SKU').fill('a')

    await expect(page.getByText('Type at least 2 characters')).toBeVisible()
    await page.waitForTimeout(500)
    expect(searches).toEqual([])
  })

  test('[US-F026][AC3] min price above max price shows a field error and sends nothing', async ({
    page,
  }) => {
    await signIn(page)
    const filtered: string[] = []
    page.on('request', (request) => {
      if (request.url().includes('minPrice=')) filtered.push(request.url())
    })

    await page.getByLabel('Min price').fill('50')
    await page.getByLabel('Max price').fill('10')
    await page.getByRole('button', { name: 'Apply filters' }).click()

    await expect(page.getByLabel('Min price')).toHaveAccessibleDescription(
      'Min price must be less than or equal to max price.',
    )
    expect(filtered).toEqual([])
  })

  test('[US-F026][AC4] a page past the end offers a link to page 1', async ({ page }) => {
    await signIn(page)
    await page.goto('/products?page=9999')

    await expect(page.getByText('No products on this page.')).toBeVisible()
    await page.getByRole('link', { name: 'Go to page 1' }).click()
    await expect(page).toHaveURL(/\/products$/)
  })
})

test.describe('product detail', () => {
  test('[US-F027][AC1] the detail page shows the product and its stock state, axe clean', async ({
    page,
  }) => {
    await signIn(page)
    await page.getByRole('heading', { level: 2 }).first().getByRole('link').click()

    // Wait for the detail route: the list page also has a level 1 heading and SKU lines (with several products
    // the assertions below would otherwise run against the list).
    await expect(page).toHaveURL(/\/products\/[^/?]+$/)
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
    await expect(page.getByText(/^SKU /)).toBeVisible()
    await expect(page.getByText(/^(In stock|Out of stock)$/).first()).toBeVisible()
    await expectNoAxeViolations(page)
  })

  test('[US-F027][AC2] an unknown product says "Product not found"', async ({ page }) => {
    await signIn(page)
    await page.goto('/products/00000000-0000-4000-8000-00000000dead')

    await expect(page.getByText('Product not found. It may have been removed.')).toBeVisible()
  })
})

test.describe('cart', () => {
  test(
    '[US-F028][AC1] adding a product updates the header count and announces it',
    { tag: '@cross-browser' },
    async ({ page }) => {
      await signIn(page)

      await page
        .getByRole('button', { name: /^Add to cart:/ })
        .first()
        .click()

      await expect(page.getByRole('status').filter({ hasText: 'in cart' })).toContainText('Added')
      await expect(page.getByRole('link', { name: /^Cart, 1 product$/ })).toBeVisible()
    },
  )

  test('[US-F028][AC3] a quantity above 1000 shows an error on the cart page, axe clean', async ({
    page,
  }) => {
    await signIn(page)
    await page
      .getByRole('button', { name: /^Add to cart:/ })
      .first()
      .click()
    await page.getByRole('link', { name: /^Cart, / }).click()

    const quantity = page.getByLabel(/^Quantity of /)
    await quantity.fill('1001')

    await expect(quantity).toHaveAccessibleDescription(/Enter a quantity from 1 to /)
    await expectNoAxeViolations(page)
  })

  test('[US-F028][AC4] the cart is empty after signing out and in again', async ({ page }) => {
    await signIn(page)
    await page
      .getByRole('button', { name: /^Add to cart:/ })
      .first()
      .click()
    await expect(page.getByRole('link', { name: /^Cart, 1 product$/ })).toBeVisible()

    await page.getByRole('button', { name: 'Sign out' }).click()
    await expect(page.getByRole('heading', { level: 1, name: 'Sign in' })).toBeVisible()
    // Nothing named cart: is left in storage.
    const keys = await page.evaluate<string[]>('Object.keys(window.localStorage)')
    expect(keys.filter((key) => key.startsWith('cart:'))).toEqual([])

    await signIn(page)
    await expect(page.getByRole('link', { name: /^Cart, 0 products$/ })).toBeVisible()
  })

  test('[US-F028][AC5] the cart survives a reload of the signed-in app', async ({ page }) => {
    await signIn(page)
    await page
      .getByRole('button', { name: /^Add to cart:/ })
      .first()
      .click()
    await expect(page.getByRole('link', { name: /^Cart, 1 product$/ })).toBeVisible()

    await page.reload()

    await expect(page.getByRole('link', { name: /^Cart, 1 product$/ })).toBeVisible()
  })
})
