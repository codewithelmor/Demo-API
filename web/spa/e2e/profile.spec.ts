import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'
import { recordCsp, requireEnvForFile, requireEnvForTest } from './support'

// Profile, change password, delete account and sign out everywhere (US-F033..F036) against the compose stack.
// These need a confirmed Customer account that exists in the stack. Set E2E_CUSTOMER_EMAIL and
// E2E_CUSTOMER_PASSWORD (generated test data, never a real customer). Without them the tests skip.
// The destructive journeys (change password, delete account) need their own throw-away account:
// set E2E_THROWAWAY_EMAIL/_PASSWORD (change password) and E2E_THROWAWAY_DELETE_EMAIL/_PASSWORD (erase) for those;
// two accounts so the tests do not depend on each other. They skip without them. Create them with
// `node scripts/create-e2e-throwaway.mjs`.
const email = process.env.E2E_CUSTOMER_EMAIL
const password = process.env.E2E_CUSTOMER_PASSWORD
const throwawayEmail = process.env.E2E_THROWAWAY_EMAIL
const throwawayPassword = process.env.E2E_THROWAWAY_PASSWORD
const deleteEmail = process.env.E2E_THROWAWAY_DELETE_EMAIL
const deletePassword = process.env.E2E_THROWAWAY_DELETE_PASSWORD

requireEnvForFile('E2E_CUSTOMER_EMAIL', 'E2E_CUSTOMER_PASSWORD')
// One shared account and a stack with state: the tests of a file run one after the other.
test.describe.configure({ mode: 'serial' })

async function signIn(page: Page, address = email, secret = password) {
  await page.goto('/sign-in')
  await page.getByLabel('Email').fill(address ?? '')
  await page.getByLabel('Password', { exact: true }).fill(secret ?? '')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page.getByRole('heading', { level: 1, name: 'Products' })).toBeVisible()
}

async function expectNoAxeViolations(page: Page) {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag22aa'])
    .analyze()
  expect(results.violations).toEqual([])
}

test.describe('profile', () => {
  test('[US-F033][AC1] saves a new phone and shows it on My account', async ({ page }) => {
    await signIn(page)
    await page.getByRole('link', { name: 'My account' }).click()
    await expectNoAxeViolations(page)
    await page.getByRole('link', { name: 'Edit profile' }).click()
    await expectNoAxeViolations(page)

    await page.getByLabel('Phone (optional)').fill('+14155550123')
    await page.getByRole('button', { name: 'Save changes' }).click()

    await expect(page.getByRole('heading', { level: 1, name: 'My account' })).toBeVisible()
    await expect(page.getByText('Profile saved.')).toBeVisible()
    await expect(page.getByText('+14155550123')).toBeVisible()
  })

  test('[US-F033][AC2] a change made in another tab shows the conflict and keeps my value', async ({
    page,
    context,
  }) => {
    await signIn(page)
    await page.goto('/account/edit')
    await page.getByLabel('Phone (optional)').fill('+14155550111')

    // Another tab saves first.
    const other = await context.newPage()
    await other.goto('/account/edit')
    await other.getByLabel('Phone (optional)').fill('+14155550222')
    await other.getByRole('button', { name: 'Save changes' }).click()
    await expect(other.getByRole('heading', { level: 1, name: 'My account' })).toBeVisible()

    await page.getByRole('button', { name: 'Save changes' }).click()

    await expect(page.getByText('This record changed since you opened it.')).toBeVisible()
    await expect(page.getByLabel('Phone (optional)')).toHaveValue('+14155550222')
    await expect(page.getByText('Your change: +14155550111')).toBeVisible()
    await expectNoAxeViolations(page)
  })

  test('[US-F033][AC3] an empty phone clears it', async ({ page }) => {
    await signIn(page)
    await page.goto('/account/edit')

    await page.getByLabel('Phone (optional)').fill('')
    await page.getByRole('button', { name: 'Save changes' }).click()

    await expect(page.getByText('Not added')).toBeVisible()
  })
})

test.describe('change password', () => {
  test('[US-F034][AC2] a wrong current password shows the error with the lockout warning', async ({
    page,
  }) => {
    await signIn(page)
    await page.goto('/account/password')
    await expectNoAxeViolations(page)

    await page.getByLabel('Current password').fill('wrong sample password')
    await page.getByLabel('New password').fill('a brand new sample passphrase')
    await page.getByRole('button', { name: 'Change password' }).click()

    await expect(page.getByText(/After 5 failed attempts your account is locked/)).toBeVisible()
    await expect(page.getByLabel('Current password')).toBeFocused()
  })

  test('[US-F034][AC1] a new password signs the user out with a message', async ({ page }) => {
    requireEnvForTest('E2E_THROWAWAY_EMAIL', 'E2E_THROWAWAY_PASSWORD')
    await signIn(page, throwawayEmail, throwawayPassword)
    await page.goto('/account/password')

    await page.getByLabel('Current password').fill(throwawayPassword ?? '')
    await page.getByLabel('New password').fill(`${throwawayPassword ?? ''} again`)
    await page.getByRole('button', { name: 'Change password' }).click()

    await expect(page.getByRole('heading', { level: 1, name: 'Sign in' })).toBeVisible()
    await expect(page.getByText('Password changed. Sign in with your new password.')).toBeVisible()
  })
})

test.describe('delete account', () => {
  test('[US-F035][AC3] a wrong password shows the error on the password field', async ({
    page,
  }) => {
    await signIn(page)
    await page.goto('/account/delete')
    await expectNoAxeViolations(page)

    await page.getByLabel('Current password').fill('wrong sample password')
    await page.getByLabel('I understand this cannot be undone.').check()
    await page.getByRole('button', { name: 'Delete my account' }).click()
    const dialog = page.getByRole('alertdialog', { name: 'Delete your account permanently?' })
    await expectNoAxeViolations(page)
    await dialog.getByRole('button', { name: 'Delete account' }).click()

    await expect(page.getByLabel('Current password')).toBeFocused()
    await expect(page.getByRole('alertdialog')).toHaveCount(0)
  })

  // Finding L-3: opening a Radix modal must cause no CSP violation (style-src nonce from the BFF, Zod without
  // eval, see src/lib/zod.ts and src/app/csp-nonce.ts).
  test('[L-3] opening and closing the delete dialog causes no CSP violation', async ({ page }) => {
    const csp = await recordCsp(page)
    await signIn(page)
    await page.goto('/account/delete')
    await page.getByLabel('Current password').fill('wrong sample password')
    await page.getByLabel('I understand this cannot be undone.').check()
    await page.getByRole('button', { name: 'Delete my account' }).click()
    await expect(
      page.getByRole('alertdialog', { name: 'Delete your account permanently?' }),
    ).toBeVisible()
    await page.keyboard.press('Escape')
    await expect(page.getByRole('alertdialog')).toHaveCount(0)

    expect(csp.violations).toEqual([])
    expect(csp.consoleErrors).toEqual([])
  })

  test('[US-F035][AC1] erases the account and ends the session', async ({ page }) => {
    requireEnvForTest('E2E_THROWAWAY_DELETE_EMAIL', 'E2E_THROWAWAY_DELETE_PASSWORD')
    await signIn(page, deleteEmail, deletePassword)
    await page.goto('/account/delete')

    await page.getByLabel('Current password').fill(deletePassword ?? '')
    await page.getByLabel('I understand this cannot be undone.').check()
    await page.getByRole('button', { name: 'Delete my account' }).click()
    await page.getByRole('alertdialog').getByRole('button', { name: 'Delete account' }).click()

    await expect(page.getByRole('heading', { level: 1, name: 'Sign in' })).toBeVisible()
    await expect(page.getByText('Your account was erased.')).toBeVisible()
  })
})

test.describe('sign out everywhere', () => {
  test('[US-F036][AC1] ends the session in the other tab on its next request', async ({
    page,
    context,
  }) => {
    await signIn(page)
    const other = await context.newPage()
    await other.goto('/account')
    await expect(other.getByRole('heading', { level: 1, name: 'My account' })).toBeVisible()

    await page.goto('/account')
    await page.getByRole('button', { name: 'Sign out everywhere' }).click()
    await expect(page.getByRole('heading', { level: 1, name: 'Sign in' })).toBeVisible()

    // The cookie is shared, so the other tab is signed out too when it asks again.
    await other.reload()
    await expect(other.getByRole('heading', { level: 1, name: 'Sign in' })).toBeVisible()
  })
})
