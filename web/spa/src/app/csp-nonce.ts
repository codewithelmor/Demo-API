/**
 * Passes the Content Security Policy (CSP) "nonce" to the libraries that inject `<style>` tags.
 *
 * Background: the server tells the browser which inline styles it may apply (a CSP). It does so with a
 * random one-time value, the nonce, and only `<style>` tags carrying that value are allowed.
 * This module runs once when the app starts (see the call at the bottom of the file).
 */
/** The text index.html contains before the BFF replaces it with a real nonce. Seeing it means "no server in front". */
const PLACEHOLDER = '__CSP_NONCE__'

/** The one global property that libraries read to find the nonce (a webpack convention). */
interface NonceTarget {
  __webpack_nonce__?: string
}

/**
 * Reads the nonce from `<meta name="csp-nonce">` and stores it on `globalThis.__webpack_nonce__`.
 *
 * The BFF (the ASP.NET Core server that hosts this app) puts a fresh nonce in that meta tag on every
 * index.html response and allows it in `style-src`. Radix's scroll lock (react-style-singleton) reads
 * `__webpack_nonce__` when it injects its `<style>`, so without this the browser would block it.
 * The Vite dev server has no BFF in front, so the placeholder stays; then nothing is set.
 *
 * @param doc    The document to read the meta tag from. Defaults to the real page; tests pass a fake one.
 * @param target Where to store the nonce. Defaults to `globalThis`; tests pass a plain object.
 */
export function applyCspNonce(doc: Document = document, target?: NonceTarget): void {
  const holder: NonceTarget = target ?? (globalThis as NonceTarget)
  const value = doc.querySelector('meta[name="csp-nonce"]')?.getAttribute('content')?.trim()
  if (value && value !== PLACEHOLDER) {
    holder.__webpack_nonce__ = value
  }
}

applyCspNonce()
