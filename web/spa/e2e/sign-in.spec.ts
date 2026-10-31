import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'

// US-F002 / NFR-F01: the sign-in page renders for an anonymous visitor and has no axe violations.
test(
  'sign-in page has no accessibility violations @US-F002',
  { tag: '@cross-browser' },
  async ({ page }) => {
    await page.goto('/sign-in')
    await expect(page.getByRole('heading', { level: 1, name: 'Sign in' })).toBeVisible()
    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag22aa'])
      .analyze()
    expect(results.violations).toEqual([])
  },
)
