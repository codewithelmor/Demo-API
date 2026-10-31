import js from '@eslint/js'
import pluginQuery from '@tanstack/eslint-plugin-query'
import jsxA11y from 'eslint-plugin-jsx-a11y'
import reactHooks from 'eslint-plugin-react-hooks'
import { defineConfig, globalIgnores } from 'eslint/config'
import globals from 'globals'
import tseslint from 'typescript-eslint'

// Feature folders (frontend-architecture.md section 2). A feature never imports another feature.
const features = ['auth', 'catalog', 'cart', 'orders', 'profile', 'admin']

// Gitignore-style groups also match nested paths, so a feature folder itself (a barrel) is a regex.
const noFeatureBarrel = {
  regex: '^@/features/[^/]+$',
  message: 'No barrel files. Import the module that defines the symbol.',
}

const noBarrels = {
  group: ['**/index', '**/index.ts', '**/index.tsx'],
  message: 'No barrel files. Import the module that defines the symbol.',
}

// NFR-F35: XSS sinks are banned. Server text is shown as text; URLs from data need safeHttpUrl().
const xssSelectors = [
  {
    selector: "JSXAttribute[name.name='dangerouslySetInnerHTML']",
    message: 'dangerouslySetInnerHTML is banned (NFR-F35). Render server text as text.',
  },
  {
    selector: 'AssignmentExpression[left.property.name=/^(innerHTML|outerHTML)$/]',
    message: 'innerHTML and outerHTML are banned (NFR-F35).',
  },
  {
    selector: "CallExpression[callee.property.name='insertAdjacentHTML']",
    message: 'insertAdjacentHTML is banned (NFR-F35).',
  },
  {
    selector: "CallExpression[callee.object.name='document'][callee.property.name=/^write(ln)?$/]",
    message: 'document.write is banned (NFR-F35).',
  },
  {
    selector: "NewExpression[callee.name='Function'], CallExpression[callee.name='Function']",
    message: 'new Function is banned (NFR-F35).',
  },
  {
    selector:
      "JSXAttribute[name.name=/^(href|src)$/][value.type='JSXExpressionContainer']:not([value.expression.callee.name='safeHttpUrl'])",
    message:
      'href and src from data must pass through safeHttpUrl() from @/lib/safe-url (NFR-F35). Use a string literal or <Link to>.',
  },
  {
    selector:
      "JSXAttribute[name.name=/^(action|formAction|srcDoc)$/][value.type='JSXExpressionContainer']",
    message: 'action, formAction and srcDoc are URL or HTML sinks (NFR-F35). Use a string literal.',
  },
  {
    selector:
      "JSXAttribute[name.name='to'][value.type='Literal'][value.value=/^(https?|javascript|data):/i]",
    message:
      '<Link to> must be an in-app path, not an absolute or protocol-relative URL (NFR-F34).',
  },
  {
    selector:
      "CallExpression[callee.property.name='setAttribute'][arguments.0.value=/^(href|src|srcdoc|action|formaction|xlink:href)$/i]",
    message: 'setAttribute on a URL or HTML attribute is banned (NFR-F35).',
  },
  {
    selector:
      "AssignmentExpression[left.type='MemberExpression'][left.property.name=/^(href|src|srcdoc|action|formAction)$/]",
    message:
      'Assigning a URL or HTML property is banned (NFR-F35). Use <a href>, <Link> or navigate().',
  },
  {
    selector:
      "AssignmentExpression[left.name='location'], AssignmentExpression[left.property.name='location']",
    message: 'Assigning location is banned (NFR-F34). Use navigate() with a checked in-app path.',
  },
  {
    selector:
      "CallExpression[callee.property.name=/^(assign|replace)$/][callee.object.property.name='location'], CallExpression[callee.property.name=/^(assign|replace)$/][callee.object.name='location']",
    message: 'location.assign and location.replace are banned (NFR-F34). Use navigate().',
  },
  {
    selector:
      "CallExpression[callee.property.name='open'][callee.object.name=/^(window|globalThis|self)$/], CallExpression[callee.name='open']",
    message: 'window.open is banned (NFR-F34).',
  },
]

export default defineConfig([
  globalIgnores([
    'dist',
    'src/api/schema.d.ts',
    'coverage',
    'playwright-report',
    'test-results',
    // Code that must FAIL these rules; src/lint-fixtures/sinks.test.ts lints it on purpose.
    'src/lint-fixtures/sinks.tsx',
  ]),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.strictTypeChecked,
      // Rules of Hooks plus the React Compiler rules (purity, refs, set state in render, and more).
      reactHooks.configs.flat.recommended,
      jsxA11y.flatConfigs.strict,
      pluginQuery.configs['flat/recommended'],
    ],
    languageOptions: {
      globals: globals.browser,
      parserOptions: { projectService: true, tsconfigRootDir: import.meta.dirname },
    },
    rules: {
      '@typescript-eslint/restrict-template-expressions': ['error', { allowNumber: true }],
      'no-eval': 'error',
      'no-implied-eval': 'error',
      'no-new-func': 'error',
      'no-script-url': 'error',
      'no-restricted-syntax': ['error', ...xssSelectors],
      'no-restricted-imports': ['error', { patterns: [noFeatureBarrel, noBarrels] }],
    },
  },
  // No feature imports another feature's internals.
  ...features.map((feature) => ({
    files: [`src/features/${feature}/**/*.{ts,tsx}`],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            noFeatureBarrel,
            noBarrels,
            ...features
              .filter((other) => other !== feature)
              .map((other) => ({
                group: [`@/features/${other}/**`, `**/features/${other}/**`, `../${other}/**`],
                message: `features/${feature} must not import features/${other}. Share through components/, hooks/ or lib/.`,
              })),
          ],
        },
      ],
    },
  })),
  // Nothing outside features/admin imports it, except the router through lazy() (dynamic import).
  // (Feature folders get the same rule above, because admin is in their "other features" list.)
  {
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/features/**'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            noFeatureBarrel,
            noBarrels,
            {
              group: ['@/features/admin/**', '**/features/admin/**'],
              message:
                'Only features/admin may import admin code. The router loads it with lazy().',
            },
          ],
        },
      ],
    },
  },
  // Shared layers (hooks, components, lib, api) never import a feature: features import them, not the reverse.
  {
    files: ['src/{hooks,components,lib,api}/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            noFeatureBarrel,
            noBarrels,
            {
              group: ['@/features/**', '**/features/**'],
              message: 'Shared code (hooks, components, lib, api) must not import a feature.',
            },
          ],
        },
      ],
    },
  },
  {
    files: ['*.config.ts', 'e2e/**/*.ts'],
    languageOptions: { globals: globals.node },
  },
])
