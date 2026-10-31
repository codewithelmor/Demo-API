import { describe, expect, it } from 'vitest'
import { z } from '@/lib/zod'

const sources = import.meta.glob<string>('/src/**/*.{ts,tsx}', {
  query: '?raw',
  import: 'default',
  eager: true,
})

describe('zod without eval [L-3]', () => {
  it('turns the JIT off', () => {
    expect(z.core.globalConfig.jitless).toBe(true)
  })

  it('no file imports zod at runtime except src/lib/zod.ts (a type-only import is fine)', () => {
    const offenders = Object.entries(sources)
      .filter(([path]) => path !== '/src/lib/zod.ts' && !path.includes('.test.'))
      .filter(([, text]) => /^import (?!type )[^\n]*from 'zod'/m.test(text))
      .map(([path]) => path)
    expect(offenders).toEqual([])
  })
})
