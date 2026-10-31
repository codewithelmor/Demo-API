// Returns the URL only when it is absolute http(s); otherwise undefined (NFR-F35).
// Use it for every href or src that comes from data.
export function safeHttpUrl(value: string | null | undefined): string | undefined {
  if (!value) return undefined
  try {
    const url = new URL(value)
    return url.protocol === 'https:' || url.protocol === 'http:' ? url.href : undefined
  } catch {
    return undefined
  }
}

// A fixed placeholder origin: the value is resolved against it, and anything that ends up on another origin is refused.
const BASE = 'https://app.invalid'

function hasBadChar(text: string): boolean {
  for (let i = 0; i < text.length; i++) {
    const code = text.charCodeAt(i)
    // Control characters, DEL and backslash (browsers treat "\" like "/").
    if (code < 32 || code === 127 || code === 92) return true
  }
  return false
}

// A same-origin relative path for post-sign-in redirects (NFR-F34, FT-11).
// "/products?page=2" is accepted. Refused: other origins, "//host" (also as %2f%2f), backslashes, control
// characters, and the /api and /bff paths (also with dot segments or encoding).
// Whether the user's role may open the path is decided by returnTargetFor() in lib/session.
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
