import { ESLint } from 'eslint'
import { describe, expect, it } from 'vitest'

// The XSS and redirect lint rules (NFR-F34, NFR-F35) only protect us if they really fire. This lints a file full
// of banned sinks (ignored by the normal lint run) and expects each one to be reported.
describe('lint rules for XSS and redirect sinks', () => {
  it('reports every banned sink in the fixture', async () => {
    const eslint = new ESLint({ ignore: false })
    const [result] = await eslint.lintFiles(['src/lint-fixtures/sinks.tsx'])
    const reported = (result?.messages ?? [])
      .filter((message) => message.ruleId === 'no-restricted-syntax')
      .map((message) => message.message)

    const expected = [
      'innerHTML and outerHTML are banned',
      'insertAdjacentHTML is banned',
      'setAttribute on a URL or HTML attribute is banned',
      'Assigning a URL or HTML property is banned',
      'location.assign and location.replace are banned',
      'window.open is banned',
      'document.write is banned',
      'dangerouslySetInnerHTML is banned',
      'href and src from data must pass through safeHttpUrl()',
      'action, formAction and srcDoc are URL or HTML sinks',
      '<Link to> must be an in-app path',
    ]
    for (const text of expected) {
      expect(
        reported.some((message) => message.includes(text)),
        text,
      ).toBe(true)
    }
    // srcDoc and action are two separate reports.
    expect(reported.filter((message) => message.includes('URL or HTML sinks'))).toHaveLength(2)
  })
})
