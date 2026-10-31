import { test, type Page } from '@playwright/test'

// E2E_* variables. Locally a missing variable skips the test that needs it. In CI it FAILS: a pipeline must
// never go green because the tests quietly did not run.
function missingMessage(names: string[]): string | undefined {
  const missing = names.filter((name) => !process.env[name])
  return missing.length === 0 ? undefined : `Set ${missing.join(' and ')} to run these tests.`
}

// For a whole file. Call it at the top level of the spec.
export function requireEnvForFile(...names: string[]): void {
  const message = missingMessage(names)
  if (message === undefined) return
  if (process.env.CI) {
    test.beforeAll(() => {
      throw new Error(`${message} (CI is set, so this fails instead of skipping.)`)
    })
  } else {
    test.skip(true, message)
  }
}

// For one test (the destructive ones). Call it first in the test body.
export function requireEnvForTest(...names: string[]): void {
  const message = missingMessage(names)
  if (message === undefined) return
  if (process.env.CI) throw new Error(`${message} (CI is set, so this fails instead of skipping.)`)
  test.skip(true, message)
}

// Records every CSP violation the page reports (securitypolicyviolation events) and every console message that
// mentions the Content Security Policy. Call it BEFORE the first page.goto. The events go through an exposed
// function, so they survive full page navigations. A test asserts both lists are empty (security finding L-3).
export interface CspRecorder {
  violations: string[]
  consoleErrors: string[]
}

export async function recordCsp(page: Page): Promise<CspRecorder> {
  const recorder: CspRecorder = { violations: [], consoleErrors: [] }
  await page.exposeFunction('__reportCsp', (detail: string) => {
    recorder.violations.push(detail)
  })
  // A string, not a function: the e2e tsconfig has no DOM types.
  await page.addInitScript(`
    document.addEventListener('securitypolicyviolation', (event) => {
      window.__reportCsp(event.violatedDirective + ' blocked=' + event.blockedURI + ' sample=' + event.sample)
    })
  `)
  page.on('console', (message) => {
    if (message.type() === 'error' && /content security policy|refused to/i.test(message.text())) {
      recorder.consoleErrors.push(message.text())
    }
  })
  return recorder
}
