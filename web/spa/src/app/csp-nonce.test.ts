import { describe, expect, it } from 'vitest'
import { applyCspNonce } from '@/app/csp-nonce'

function documentWith(content: string | null): Document {
  const doc = document.implementation.createHTMLDocument('test')
  if (content !== null) {
    const meta = doc.createElement('meta')
    meta.name = 'csp-nonce'
    meta.content = content
    doc.head.append(meta)
  }
  return doc
}

describe('applyCspNonce', () => {
  it('copies the nonce from the meta tag for libraries that read __webpack_nonce__', () => {
    const target: { __webpack_nonce__?: string } = {}
    applyCspNonce(documentWith('abc123-_XYZ'), target)
    expect(target.__webpack_nonce__).toBe('abc123-_XYZ')
  })

  it('ignores the placeholder the Vite dev server leaves in place', () => {
    const target: { __webpack_nonce__?: string } = {}
    applyCspNonce(documentWith('__CSP_NONCE__'), target)
    expect(target.__webpack_nonce__).toBeUndefined()
  })

  it('ignores a missing or empty meta tag', () => {
    const missing: { __webpack_nonce__?: string } = {}
    const empty: { __webpack_nonce__?: string } = {}
    applyCspNonce(documentWith(null), missing)
    applyCspNonce(documentWith('  '), empty)
    expect(missing.__webpack_nonce__).toBeUndefined()
    expect(empty.__webpack_nonce__).toBeUndefined()
  })
})
