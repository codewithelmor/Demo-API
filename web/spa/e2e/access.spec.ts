import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'
import { randomUUID } from 'node:crypto'

// Anonymous access pages (US-F020..F025) against the compose stack. Generated data only.
// The BFF allows 10 auth calls per minute per IP (US-F008); each test here makes at most one,
// so the suite stays under the limit. Confirm and reset use links that cannot be valid (the real
// tokens are only in the API log), which is the "invalid or expired" path.

const uniqueEmail = () => `e2e.${randomUUID().slice(0, 8)}@example.test`
const passphrase = 'correct horse battery staple'

async function expectNoAxeViolations(page: Page) {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag22aa'])
    .analyze()
  expect(results.violations).toEqual([])
}

test.describe('register', () => {
  test('[US-F020][AC1] a valid form ends on "Check your email", axe clean', async ({ page }) => {
    await page.goto('/register')
    await expect(page.getByRole('heading', { level: 1, name: 'Create your account' })).toBeVisible()
    await expectNoAxeViolations(page)

    await page.getByLabel('First name').fill('Sample')
    await page.getByLabel('Last name').fill('Person')
    await page.getByLabel('Email').fill(uniqueEmail())
    await page.getByLabel('Password', { exact: true }).fill(passphrase)
    await page.getByRole('button', { name: 'Create account' }).click()

    await expect(page).toHaveURL(/\/register\/check-email$/)
    await expect(page.getByRole('heading', { level: 1, name: 'Check your email' })).toBeVisible()
    await expectNoAxeViolations(page)
  })

  test(
    '[US-F020][AC2] a short password shows the rule when leaving the field and sends nothing',
    { tag: '@cross-browser' },
    async ({ page }) => {
      let registerCalls = 0
      page.on('request', (request) => {
        if (request.url().includes('/api/v1/auth/register')) registerCalls++
      })
      await page.goto('/register')

      const password = page.getByLabel('Password', { exact: true })
      await password.fill('too short')
      await password.blur()
      await expect(page.getByText(/Use at least 15 characters/)).toBeVisible()

      await page.getByRole('button', { name: 'Create account' }).click()
      expect(registerCalls).toBe(0)
      await expectNoAxeViolations(page)
    },
  )

  test('[US-F020][AC4] a phone without the country code shows the E.164 hint', async ({ page }) => {
    await page.goto('/register')
    await page.getByLabel('First name').fill('Sample')
    await page.getByLabel('Last name').fill('Person')
    await page.getByLabel('Email').fill(uniqueEmail())
    await page.getByLabel('Password', { exact: true }).fill(passphrase)
    await page.getByLabel('Phone (optional)').fill('4155550123')
    await page.getByRole('button', { name: 'Create account' }).click()

    await expect(page.getByLabel('Phone (optional)')).toBeFocused()
    await expect(page.getByLabel('Phone (optional)')).toHaveAccessibleDescription(
      /with \+ and the country code/,
    )
  })
})

test.describe('confirm email', () => {
  test('[US-F021][AC2] an invalid link says so, offers resend, and hides the token', async ({
    page,
  }) => {
    await page.goto(`/confirm-email?userId=${randomUUID()}&token=not-a-real-token`)

    await expect(
      page.getByRole('heading', { level: 1, name: 'This link is invalid or expired' }),
    ).toBeVisible()
    await expect(page.getByRole('button', { name: 'Send new link' })).toBeVisible()
    // The token never stays in the address bar (NFR-F43).
    await expect(page).toHaveURL(/\/confirm-email$/)
    await expectNoAxeViolations(page)
  })
})

test.describe('resend confirmation', () => {
  test('[US-F022][AC1] the same neutral message for any email, axe clean', async ({ page }) => {
    await page.goto('/resend-confirmation')
    await page.getByLabel('Email').fill(uniqueEmail())
    await page.getByRole('button', { name: 'Send confirmation email' }).click()

    await expect(page.getByRole('status')).toHaveText(
      'If an account needs confirming, we sent a new link.',
    )
    await expectNoAxeViolations(page)
  })

  test('[US-F022][AC1] sign-in links to resend', async ({ page }) => {
    await page.goto('/sign-in')
    await page.getByRole('link', { name: 'Resend it' }).click()
    await expect(
      page.getByRole('heading', { level: 1, name: 'Resend confirmation email' }),
    ).toBeVisible()
  })
})

test.describe('forgot password', () => {
  test('[US-F024][AC1] the same message for any email, axe clean', async ({ page }) => {
    await page.goto('/forgot-password')
    await expect(page.getByRole('heading', { level: 1, name: 'Reset your password' })).toBeVisible()
    await expectNoAxeViolations(page)

    await page.getByLabel('Email').fill(uniqueEmail())
    await page.getByRole('button', { name: 'Send reset link' }).click()

    await expect(page.getByRole('status')).toHaveText(
      'If an account exists for this email, we sent a link. It works for 1 hour.',
    )
  })

  test('[US-F024][AC2] an invalid email shows a field error and makes no call', async ({
    page,
  }) => {
    let calls = 0
    page.on('request', (request) => {
      if (request.url().includes('/api/v1/auth/forgot-password')) calls++
    })
    await page.goto('/forgot-password')
    await page.getByLabel('Email').fill('not-an-email')
    await page.getByRole('button', { name: 'Send reset link' }).click()

    await expect(page.getByLabel('Email')).toHaveAccessibleDescription(
      'Enter a valid email address.',
    )
    expect(calls).toBe(0)
  })
})

test.describe('reset password', () => {
  test('[US-F025][AC3] the token is gone from the address bar once the page has loaded', async ({
    page,
  }) => {
    await page.goto(`/reset-password?email=${encodeURIComponent(uniqueEmail())}&token=sample-token`)

    await expect(page.getByRole('heading', { level: 1, name: 'Set a new password' })).toBeVisible()
    await expect(page).toHaveURL(/\/reset-password$/)
    await expectNoAxeViolations(page)
  })

  test('[US-F025][AC2] an invalid link says so and offers a new one', async ({ page }) => {
    await page.goto(`/reset-password?email=${encodeURIComponent(uniqueEmail())}&token=bad-token`)
    await page.getByLabel('New password').fill(passphrase)
    await page.getByRole('button', { name: 'Set new password' }).click()

    await expect(page.getByText('This link is invalid or expired.')).toBeVisible()
    await expect(page.getByRole('link', { name: 'Request new link' })).toHaveAttribute(
      'href',
      '/forgot-password',
    )
  })

  test('[US-F025][AC3] a refreshed page asks to open the email link again', async ({ page }) => {
    await page.goto(`/reset-password?email=${encodeURIComponent(uniqueEmail())}&token=sample-token`)
    await expect(page.getByRole('heading', { level: 1, name: 'Set a new password' })).toBeVisible()
    await page.reload()

    await expect(page.getByText(/Open the link from your email again/)).toBeVisible()
  })
})
