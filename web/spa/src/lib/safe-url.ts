/**
 * Checks a URL that came from data before it is put in an `href` or `src` (NFR-F35).
 * Without a check, a value like `javascript:alert(1)` could run code when clicked (XSS).
 * A lint rule forces every data-driven `href`/`src` through this function.
 *
 * @returns The normalised URL when it is an absolute http(s) address; otherwise `undefined`.
 */
export function safeHttpUrl(value: string | null | undefined): string | undefined {
  if (!value) return undefined
  try {
    const url = new URL(value)
    return url.protocol === 'https:' || url.protocol === 'http:' ? url.href : undefined
  } catch {
    return undefined
  }
}

/** Placeholder web address used only to resolve relative paths while checking them. */
// A fixed placeholder origin: the value is resolved against it, and anything that ends up on another origin is refused.
const BASE = 'https://app.invalid'

/** True when the text has a character that is never legitimate in an in-app path. */
function hasBadChar(text: string): boolean {
  for (let i = 0; i < text.length; i++) {
    const code = text.charCodeAt(i)
    // Control characters, DEL and backslash (browsers treat "\" like "/").
    if (code < 32 || code === 127 || code === 92) return true
  }
  return false
}

/**
 * Checks the `returnUrl` (where to go after signing in) so it can only point inside this app (NFR-F34, FT-11).
 * This prevents an "open redirect", where a crafted sign-in link would send the user to another website.
 *
 * `"/products?page=2"` is accepted. Refused: other origins, `"//host"` (also as `%2f%2f`), backslashes,
 * control characters, and the `/api` and `/bff` paths (also with dot segments or encoding).
 * Whether the user's role may open the path is decided by `returnTargetFor()` in `lib/session`.
 *
 * @returns The cleaned path with its query and hash, or `undefined` when it is not safe.
 */
export function safeReturnUrl(value: string | null | undefined): string | undefined {
  if (!value?.startsWith('/') || hasBadChar(value)) return undefined
  let url: URL
  try {
    url = new URL(value, BASE)
  } catch {
    return undefined
  }
  if (url.origin !== BASE) return undefined
  let decoded: string
  try {
    decoded = decodeURIComponent(url.pathname)
  } catch {
    return undefined
  }
  if (hasBadChar(decoded) || decoded.startsWith('//')) return undefined
  if (/^\/(api|bff)(\/|$)/i.test(decoded)) return undefined
  return `${url.pathname}${url.search}${url.hash}`
}
