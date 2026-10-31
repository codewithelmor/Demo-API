// Drives the real Orders Shop web app (BFF + SPA on http://localhost:8081) and records it with Playwright.
// Demo data only: a brand-new generated customer, plus the seeded dev Admin. No production data.
//
//   node web-record.mjs <scratch_dir> [--dry]
//
// <scratch_dir>/audio/durations.json comes from web-tts.py. Each narration segment is started together with
// the on-screen action it describes (narrate()), and the step waits until the segment has been spoken, so
// picture and voice stay in sync by construction. --dry runs every step with no waits and no video (a check run).
// Output: <scratch_dir>/rec/customer.webm, admin.webm, marks.json (start second of every segment), journal.json
// (pass/fail per step). A failing step stops the run: it is reported, never skipped or edited around.
//
// Secrets: the Admin password is read from the api container into memory, typed into the (masked) password
// field and never printed or written. The email links (one-time tokens) come from the API log and are only
// opened in the browser; they are never printed. The browser address bar is not part of the recording.
import { execFileSync } from 'node:child_process'
import { randomBytes } from 'node:crypto'
import { mkdirSync, readFileSync, writeFileSync, readdirSync, renameSync } from 'node:fs'
import { createRequire } from 'node:module'
import { join } from 'node:path'

const repo = new URL('../../../../../', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1')
const require = createRequire(join(repo, 'web/spa/package.json'))
const { chromium, expect } = require('@playwright/test')

const scratch = process.argv[2]
const dry = process.argv.includes('--dry')
const base = process.env.E2E_BASE_URL ?? 'http://localhost:8081'
const rec = join(scratch, 'rec')
mkdirSync(rec, { recursive: true })
const durations = Object.fromEntries(
  JSON.parse(readFileSync(join(scratch, 'audio', 'durations.json'), 'utf8')).map((s) => [s.id, s.dur]),
)
const sleep = (ms) => new Promise((r) => setTimeout(r, dry ? 0 : ms))

const runId = randomBytes(2).toString('hex')
const domain = `demo-${runId}.example.test`
const email = `maya@${domain}`
const pw1 = 'Quiet harbor passphrase 2026'
const pw2 = 'Bright meadow passphrase 2026'
const pw3 = 'Silver river passphrase 2026'
const journal = []
const marks = { customer: {}, admin: {} }
let t0 = 0
let side = 'customer'
let current

// A visible cursor and click ring (Playwright videos do not draw the mouse). Uses CSSOM only (CSP safe).
const cursorScript = `
(() => {
  const add = () => {
    if (document.getElementById('demo-cursor') || !document.documentElement) return
    const c = document.createElement('div'); c.id = 'demo-cursor'
    Object.assign(c.style, { position: 'fixed', left: '0px', top: '0px', width: '22px', height: '22px',
      marginLeft: '-11px', marginTop: '-11px', borderRadius: '50%', background: 'rgba(37,99,235,0.35)',
      border: '2px solid rgb(37,99,235)', zIndex: '2147483647', pointerEvents: 'none', transition: 'transform 80ms' })
    document.documentElement.appendChild(c)
    addEventListener('mousemove', (e) => { c.style.left = e.clientX + 'px'; c.style.top = e.clientY + 'px' }, true)
    addEventListener('mousedown', () => { c.style.transform = 'scale(0.6)' }, true)
    addEventListener('mouseup', () => { c.style.transform = 'scale(1)' }, true)
  }
  addEventListener('DOMContentLoaded', add); add()
})()`

async function step(name, fn) {
  try {
    await fn()
    journal.push({ step: name, result: 'pass' })
    console.log('PASS', name)
  } catch (error) {
    await current?.screenshot({ path: join(rec, `fail-${name}.png`) }).catch(() => undefined)
    journal.push({ step: name, result: 'FAIL', error: String(error.message ?? error).split('\n')[0] })
    writeFileSync(join(rec, 'journal.json'), JSON.stringify(journal, null, 1))
    console.log('FAIL', name, '-', String(error.message ?? error).split('\n')[0])
    throw error
  }
}

// Starts the segment, runs the action, then waits until the segment has been spoken (plus a short pause).
async function narrate(id, fn = async () => {}) {
  const start = Date.now()
  marks[side][id] = (start - t0) / 1000
  await step(id, fn)
  const wait = (durations[id] ?? 0) * 1000 + 700 - (Date.now() - start)
  if (wait > 0) await sleep(wait)
}

async function move(page, locator) {
  let box = null
  for (let attempt = 0; attempt < 4 && !box; attempt++) {
    try {
      await locator.scrollIntoViewIfNeeded()
      box = await locator.boundingBox()
    } catch (error) {
      // The page re-rendered while we looked (route change): look again.
      if (attempt === 3) throw error
      await new Promise((r) => setTimeout(r, 300))
    }
  }
  if (!box) throw new Error('element has no box')
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2, { steps: dry ? 1 : 22 })
  await sleep(200)
}
async function click(page, locator) {
  await move(page, locator)
  await locator.click()
}
async function type(page, locator, text) {
  await click(page, locator)
  await locator.fill('')
  await locator.pressSequentially(text, { delay: dry ? 0 : 55 })
}

// Reads the newest email link for this demo account from the API log (fake e-mail sender). Never printed.
async function emailLink(kind, since) {
  const mask = `m***@${domain}`
  for (let i = 0; i < 40; i++) {
    const log = execFileSync('docker', ['compose', 'logs', 'api', '--since', since, '--no-log-prefix'], {
      cwd: repo, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024,
    })
    const line = log.split('\n').reverse().find((l) => l.includes(kind) && l.includes(mask))
    const url = line?.match(/https?:\/\/\S+/)?.[0]
    if (url) return url
    await new Promise((r) => setTimeout(r, 500))
  }
  throw new Error('no email link in the API log for ' + kind)
}

async function newSession(browser, name) {
  const context = await browser.newContext({
    viewport: { width: 1280, height: 720 },
    ...(dry ? {} : { recordVideo: { dir: join(rec, name), size: { width: 1280, height: 720 } } }),
  })
  await context.addInitScript(cursorScript)
  const page = await context.newPage()
  page.setDefaultTimeout(15000)
  current = page
  t0 = Date.now()
  side = name
  return { context, page }
}
async function finish(session, name) {
  const path = dry ? undefined : await session.page.video().path()
  await session.context.close()
  if (path) renameSync(path, join(rec, `${name}.webm`))
}

async function signIn(page, who, password) {
  await type(page, page.getByLabel('Email'), who)
  await type(page, page.getByLabel('Password', { exact: true }), password)
  await click(page, page.getByRole('button', { name: 'Sign in' }))
}
const h1 = (page, name) => page.getByRole('heading', { level: 1, name })
const nav = (page) => page.getByRole('banner')

const browser = await chromium.launch()
try {
  // ---------------- Customer ----------------
  const since = new Date().toISOString()
  let s = await newSession(browser, 'customer')
  let page = s.page
  await step('open sign-in', async () => {
    await page.goto(base + '/sign-in')
    await expect(h1(page, 'Sign in')).toBeVisible()
  })
  await sleep(600)

  await narrate('c1_open', async () => {
    await click(page, page.getByRole('link', { name: 'Create account' }))
    await expect(h1(page, 'Create your account')).toBeVisible()
  })
  await narrate('c1_short', async () => {
    await type(page, page.getByLabel('First name'), 'Maya')
    await type(page, page.getByLabel('Last name'), 'Demo')
    await type(page, page.getByLabel('Email'), email)
    await type(page, page.getByLabel('Password', { exact: true }), 'short')
    await click(page, page.getByRole('button', { name: 'Create account' }))
    await expect(page.getByLabel('Password', { exact: true })).toHaveAttribute('aria-invalid', 'true')
  })
  await narrate('c1_ok', async () => {
    await type(page, page.getByLabel('Password', { exact: true }), pw1)
    await click(page, page.getByRole('button', { name: 'Create account' }))
    await expect(page).toHaveURL(/register\/check-email/)
  })
  await narrate('c1_check', async () => {
    await expect(page.getByText(/check your email/i).first()).toBeVisible()
  })
  await narrate('c1_unconf', async () => {
    await page.goto(base + '/sign-in')
    await signIn(page, email, pw1)
    await expect(page.getByRole('alert').filter({ hasText: /confirm/i })).toBeVisible()
    await click(page, page.getByRole('button', { name: 'Resend email' }))
    await expect(page.getByText('If an account exists for this email, we sent a new link.')).toBeVisible()
  })
  await narrate('c1_link', async () => {
    const link = await emailLink('Confirm email for', since)
    await page.goto(link)
    await expect(page.getByText('Email confirmed. You can now sign in.')).toBeVisible()
  })
  await narrate('c1_signin', async () => {
    await page.goto(base + '/sign-in')
    await signIn(page, email, pw1)
    await expect(h1(page, 'Products')).toBeVisible()
  })

  await narrate('c2_catalog', async () => {
    await expect(page.getByText('8 products')).toBeVisible()
    await page.mouse.move(640, 420, { steps: dry ? 1 : 30 })
  })
  await narrate('c2_search', async () => {
    const search = page.getByLabel('Search name or SKU')
    await type(page, search, 'k')
    await expect(page.getByText('Type at least 2 characters')).toBeVisible()
    await sleep(1500)
    await search.pressSequentially('ey', { delay: dry ? 0 : 150 })
    await expect(page).toHaveURL(/search=key/)
    await expect(page.getByText('1 product')).toBeVisible()
  })
  await narrate('c2_sort', async () => {
    await page.getByLabel('Search name or SKU').fill('')
    await expect(page.getByText('8 products')).toBeVisible()
    await move(page, page.getByLabel('Sort'))
    await page.getByLabel('Sort').selectOption({ label: 'Price high to low' })
    await type(page, page.getByLabel('Max price'), '100')
    await click(page, page.getByLabel('In stock only'))
    await click(page, page.getByRole('button', { name: 'Apply filters' }))
    await expect(page).toHaveURL(/maxPrice=100/)
    await expect(page.getByText('27 inch monitor')).toHaveCount(0)
  })
  await narrate('c2_detail', async () => {
    await click(page, page.getByRole('link', { name: 'Mechanical keyboard' }))
    await expect(h1(page, 'Mechanical keyboard')).toBeVisible()
    await expect(page.getByText(/^SKU /)).toBeVisible()
  })
  await narrate('c2_add', async () => {
    await click(page, page.getByRole('button', { name: /^Add to cart/ }))
    await expect(page.getByRole('link', { name: /^Cart, 1 product$/ })).toBeVisible()
    await click(page, nav(page).getByRole('link', { name: 'Products' }))
    await expect(h1(page, 'Products')).toBeVisible()
    await click(page, page.getByRole('button', { name: 'Add to cart: Wireless mouse' }))
    await expect(page.getByRole('link', { name: /^Cart, 2 products$/ })).toBeVisible()
  })

  await narrate('c3_cart', async () => {
    await click(page, page.getByRole('link', { name: /^Cart, 2 products$/ }))
    await expect(h1(page, /^Cart \(2 products\)/)).toBeVisible()
    const qty = page.getByLabel(/^Quantity of Mechanical keyboard/)
    await type(page, qty, '2')
    await expect(page.getByText(/Estimated total/)).toContainText('$')
    await click(page, page.getByRole('button', { name: /^Remove Wireless mouse/ }))
    await expect(page.getByLabel(/^Quantity of Wireless mouse/)).toHaveCount(0)
  })
  await narrate('c3_place', async () => {
    await click(page, page.getByRole('button', { name: 'Place order' }))
    await expect(h1(page, /^Order #/)).toBeVisible()
    await expect(page.getByText('Order placed. We will confirm it soon.')).toBeVisible()
    await sleep(1500)
    await page.mouse.wheel(0, 400)
  })
  await narrate('c3_list', async () => {
    await click(page, nav(page).getByRole('link', { name: 'My orders' }))
    await expect(h1(page, 'My orders')).toBeVisible()
    await click(page, page.getByLabel('Pending'))
    await click(page, page.getByRole('button', { name: 'Apply filters' }))
    await expect(page).toHaveURL(/status=Pending/)
    await expect(page.getByText('1 order')).toBeVisible()
  })
  await narrate('c3_cancel', async () => {
    await click(page, page.getByRole('table').getByRole('link').first())
    await expect(h1(page, /^Order #/)).toBeVisible()
    await click(page, page.getByRole('button', { name: 'Cancel order' }))
    const dialog = page.getByRole('alertdialog', { name: 'Cancel this order?' })
    await type(page, dialog.getByLabel('Reason (optional)'), 'Ordered by mistake')
    await click(page, dialog.getByRole('button', { name: 'Cancel order' }))
    await expect(page.getByText('Order cancelled.')).toBeVisible()
    await sleep(1200)
    await page.mouse.wheel(0, 400)
  })

  await narrate('c4_profile', async () => {
    await click(page, nav(page).getByRole('link', { name: 'My account' }))
    await expect(h1(page, 'My account')).toBeVisible()
    await click(page, page.getByRole('link', { name: 'Edit profile' }))
    await type(page, page.getByLabel('Phone (optional)'), '+14155550123')
    await click(page, page.getByRole('button', { name: 'Save changes' }))
    await expect(page.getByText('+14155550123')).toBeVisible()
  })
  await narrate('c4_pwd', async () => {
    await click(page, page.getByRole('link', { name: 'Change password' }).first())
    await type(page, page.getByLabel('Current password'), pw1)
    await type(page, page.getByLabel('New password'), pw2)
    await click(page, page.getByRole('button', { name: 'Change password' }))
    await expect(h1(page, 'Sign in')).toBeVisible()
    await signIn(page, email, pw2)
    await expect(h1(page, 'Products')).toBeVisible()
  })
  await narrate('c4_everywhere', async () => {
    await click(page, nav(page).getByRole('link', { name: 'My account' }))
    await click(page, page.getByRole('button', { name: 'Sign out everywhere' }))
    await expect(h1(page, 'Sign in')).toBeVisible()
  })
  const since2 = new Date().toISOString()
  await narrate('c4_forgot', async () => {
    await click(page, page.getByRole('link', { name: 'Forgot password?' }))
    await type(page, page.getByLabel('Email'), email)
    await click(page, page.getByRole('button', { name: 'Send reset link' }))
    await expect(page.getByText(/if an account exists|we sent|check your email/i).first()).toBeVisible()
    await page.goto(await emailLink('Password reset for', since2))
    await type(page, page.getByLabel('New password'), pw3)
    await click(page, page.getByRole('button', { name: /reset password|set .*password|save/i }))
    await expect(h1(page, 'Sign in')).toBeVisible()
    await signIn(page, email, pw3)
    await expect(h1(page, 'Products')).toBeVisible()
  })
  await narrate('c4_guard', async () => {
    await page.goto(base + '/admin')
    await expect(page.getByText('You do not have access to this page.')).toBeVisible()
  })
  await narrate('c4_delete', async () => {
    await page.goto(base + '/account')
    await click(page, page.getByRole('link', { name: 'Delete account' }).first())
    await expect(h1(page, 'Delete your account')).toBeVisible()
    await sleep(1500)
    await type(page, page.getByLabel('Current password'), pw3)
    await click(page, page.getByLabel('I understand this cannot be undone.'))
    await click(page, page.getByRole('button', { name: 'Delete my account' }))
    const confirm = page.getByRole('alertdialog', { name: 'Delete your account permanently?' })
    await expect(confirm).toBeVisible()
    await sleep(1200)
    await click(page, confirm.getByRole('button', { name: 'Delete account' }))
    await expect(h1(page, 'Sign in')).toBeVisible()
  })
  await narrate('c4_gone', async () => {
    await signIn(page, email, pw3)
    await expect(page.getByRole('alert').first()).toBeVisible()
    await expect(h1(page, 'Products')).toHaveCount(0)
  })
  await finish(s, 'customer')

  // ---------------- Admin ----------------
  const adminPassword = execFileSync(
    'docker', ['compose', 'exec', '-T', 'api', 'cat', '/run/secrets/dev/SeedAdmin__Password'],
    { cwd: repo, encoding: 'utf8', env: { ...process.env, MSYS_NO_PATHCONV: '1' } },
  ).trim()
  s = await newSession(browser, 'admin')
  page = s.page
  const productName = `Webcam Stand ${runId.toUpperCase()}`
  const sku = `DEMO-${runId.toUpperCase()}`
  await step('open sign-in (admin)', async () => {
    await page.goto(base + '/sign-in')
    await expect(h1(page, 'Sign in')).toBeVisible()
  })
  await sleep(600)
  await narrate('e_signin', async () => {
    await signIn(page, 'admin@demoapi.local', adminPassword)
    await expect(h1(page, 'Back office')).toBeVisible()
    await expect(page.getByText('Revenue')).toBeVisible()
    await page.mouse.move(700, 500, { steps: dry ? 1 : 30 })
  })
  const adminNav = () => page.getByRole('navigation', { name: 'Back office' })
  await narrate('e_list', async () => {
    await click(page, adminNav().getByRole('link', { name: 'Products' }))
    await expect(h1(page, 'Products')).toBeVisible()
    await expect(page.getByText('8 products')).toBeVisible()
    await type(page, page.getByLabel('Search name or SKU'), 'lamp')
    await expect(page.getByText('1 product')).toBeVisible()
    await page.getByLabel('Search name or SKU').fill('')
    await expect(page.getByText('8 products')).toBeVisible()
    await move(page, page.getByLabel('Sort'))
    await page.getByLabel('Sort').selectOption({ label: 'Price high to low' })
  })
  await narrate('e_new', async () => {
    await click(page, page.getByRole('link', { name: 'New product' }))
    await expect(h1(page, 'New product')).toBeVisible()
    await click(page, page.getByRole('button', { name: 'Create product' }))
    await expect(page.getByLabel('Name')).toHaveAttribute('aria-invalid', 'true')
    await type(page, page.getByLabel('Name'), productName)
    await type(page, page.getByLabel('SKU'), sku)
    await type(page, page.getByLabel('Description (optional)'), 'Adjustable stand for webcams.')
    await type(page, page.getByLabel('Price'), '19.90')
    await type(page, page.getByLabel('Stock quantity'), '40')
    await click(page, page.getByRole('button', { name: 'Create product' }))
    await expect(h1(page, 'Products')).toBeVisible()
    await type(page, page.getByLabel('Search name or SKU'), runId.toUpperCase())
    await expect(page.getByRole('row', { name: new RegExp(productName) })).toBeVisible()
  })
  await narrate('e_edit', async () => {
    await click(page, page.getByRole('link', { name: `Edit ${productName}` }))
    await expect(h1(page, 'Edit product')).toBeVisible()
    await expect(page.getByLabel('Name')).toHaveValue(productName)
    await type(page, page.getByLabel('Stock quantity'), '60')
    await click(page, page.getByRole('button', { name: 'Save changes' }))
    await expect(h1(page, 'Products')).toBeVisible()
    await type(page, page.getByLabel('Search name or SKU'), runId.toUpperCase())
    await expect(page.getByRole('row', { name: new RegExp(`${productName}.*60`) })).toBeVisible()
  })
  await narrate('e_delete', async () => {
    await click(page, page.getByRole('button', { name: `Delete ${productName}` }))
    await click(page, page.getByRole('button', { name: 'Delete product' }))
    await expect(page.getByText(`Deleted ${productName}.`)).toBeVisible()
  })
  await narrate('e_restore', async () => {
    await click(page, page.getByLabel('Include deleted'))
    await expect(page.getByRole('row', { name: new RegExp(`${productName}.*Deleted`) })).toBeVisible()
    await sleep(2500)
    await click(page, page.getByRole('button', { name: `Restore ${productName}` }))
    await expect(page.getByText(`Restored ${productName}.`)).toBeVisible()
  })
  await sleep(1000)
  await finish(s, 'admin')
  // Not recorded: put the catalog back to the 8 seeded products (soft delete through the API, like the UI does).
  const api = process.env.E2E_API_URL ?? 'http://localhost:8080'
  const headers = { 'content-type': 'application/json', 'X-CSRF': '1' }
  const login = await fetch(`${api}/api/v1/auth/login`, {
    method: 'POST', headers, body: JSON.stringify({ email: 'admin@demoapi.local', password: adminPassword }),
  })
  console.log('cleanup login', login.status)
  const token = (await login.json()).accessToken
  const auth = { ...headers, authorization: `Bearer ${token}` }
  const found = await (await fetch(`${api}/api/v1/products?search=${sku}`, { headers: { authorization: auth.authorization } })).json()
  console.log('cleanup found', found.totalCount)
  for (const item of found.items ?? []) {
    const r = await fetch(`${api}/api/v1/products/${item.id}`, { method: 'DELETE', headers: { authorization: auth.authorization, 'X-CSRF': '1' } })
    console.log('cleanup demo product', r.status)
  }
} finally {
  writeFileSync(join(rec, 'marks.json'), JSON.stringify(marks, null, 1))
  writeFileSync(join(rec, 'journal.json'), JSON.stringify(journal, null, 1))
  await browser.close()
}
console.log('done: all steps passed', journal.length)
