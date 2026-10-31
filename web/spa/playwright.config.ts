import { defineConfig, devices } from '@playwright/test'

// E2E runs against the full compose stack (BFF serves the SPA on :8081). Start it first:
//   docker compose up -d --wait
const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:8081'

export default defineConfig({
  testDir: 'e2e',
  // One shared account and one stack: files and tests run one at a time (see e2e/README notes in README.md).
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  // No automatic retries (project rule): a flaky test is fixed or quarantined.
  retries: 0,
  reporter: process.env.CI ? [['list'], ['junit', { outputFile: 'test-results/e2e.xml' }]] : 'list',
  use: {
    baseURL,
    // Traces hold typed passwords and the session cookie. Off in CI (the pipeline never publishes them anyway);
    // locally they stay in test-results/ (git-ignored).
    trace: process.env.CI ? 'off' : 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    // Chromium runs everything (also Edge). The destructive throw-away journeys run here only.
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    // Firefox runs a small stable subset tagged @cross-browser (test strategy Q-D7): sign-in, register
    // validation, catalog list, cart add, order list. Run it with `npx playwright test --project=firefox`.
    {
      name: 'firefox',
      grep: /@cross-browser/,
      use: { ...devices['Desktop Firefox'] },
    },
  ],
})
