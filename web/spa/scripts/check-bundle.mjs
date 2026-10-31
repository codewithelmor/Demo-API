// Customer initial JS budget (NFR-F03): the entry script plus every modulepreload in dist/index.html,
// each gzipped, must add up to 200 KB or less. Run after `vite build` (npm run check does).
import { readFileSync } from 'node:fs'
import { gzipSync } from 'node:zlib'

const BUDGET_BYTES = 200 * 1000
const dist = new URL('../dist/', import.meta.url)
const html = readFileSync(new URL('index.html', dist), 'utf8')

const files = new Set()
for (const match of html.matchAll(/<script[^>]*\ssrc="([^"]+\.js)"/g)) files.add(match[1])
for (const match of html.matchAll(/<link[^>]*rel="modulepreload"[^>]*\shref="([^"]+\.js)"/g)) {
  files.add(match[1])
}
if (files.size === 0) {
  console.error('check-bundle: no script found in dist/index.html. Run "npm run build" first.')
  process.exit(1)
}

let total = 0
for (const file of files) {
  const bytes = gzipSync(readFileSync(new URL(file.replace(/^[/]/, ''), dist))).length
  total += bytes
  console.log(`${String(bytes).padStart(8)}  ${file}`)
}
console.log(`${String(total).padStart(8)}  total gzip (budget ${String(BUDGET_BYTES)})`)
if (total > BUDGET_BYTES) {
  console.error('check-bundle: the initial JS is over budget.')
  process.exit(1)
}
