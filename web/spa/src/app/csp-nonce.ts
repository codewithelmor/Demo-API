const PLACEHOLDER = '__CSP_NONCE__'

interface NonceTarget {
  __webpack_nonce__?: string
}

// The BFF puts a fresh style nonce in <meta name="csp-nonce"> on every index.html response and allows it in
// style-src. Radix's scroll lock (react-style-singleton) reads `__webpack_nonce__` when it injects its <style>.
// The Vite dev server has no BFF in front, so the placeholder stays; ignore it.
export function applyCspNonce(doc: Document = document, target?: NonceTarget): void {
  const holder: NonceTarget = target ?? (globalThis as NonceTarget)
  const value = doc.querySelector('meta[name="csp-nonce"]')?.getAttribute('content')?.trim()
  if (value && value !== PLACEHOLDER) {
    holder.__webpack_nonce__ = value
  }
}

applyCspNonce()
