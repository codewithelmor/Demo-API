// Provisions everything the e2e suite needs on a fresh compose stack, through the API only:
//   - one product with stock (as the seed Admin), so the shared Customer can add it to a cart and order it;
//   - the shared confirmed Customer (E2E_CUSTOMER_*);
//   - two throw-away confirmed Customers (E2E_THROWAWAY_* is password-changed, E2E_THROWAWAY_DELETE_* is erased).
//
//   node scripts/provision-e2e-ci.mjs --vso        # Azure Pipelines: secret pipeline variables for LATER steps
//   node scripts/provision-e2e-ci.mjs [file]       # local: shell file outside the repo (default $TEMP/e2e-env.sh), then `source` it
//
// --vso prints ##vso[task.setvariable ...;issecret=true] lines. The agent consumes those lines and masks the
// values in all later log output; the values go nowhere else (no file, no artifact). Never add a console.log of a
// credential here. The API URL is E2E_API_URL (default http://localhost:8080, the port compose publishes).
// Run it once per pipeline run on a fresh stack (`docker compose up -d --wait` first).
import { writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { adminToken, createConfirmedCustomer, createStockedProduct } from './e2e-accounts.mjs'

const args = process.argv.slice(2)
const vso = args.includes('--vso')
const outFile =
  args.find((arg) => !arg.startsWith('--')) ??
  join(process.env.TEMP ?? process.env.TMPDIR ?? tmpdir(), 'e2e-env.sh')

await createStockedProduct(await adminToken())
const shared = await createConfirmedCustomer('shared')
const change = await createConfirmedCustomer('throwaway', ['Throw', 'Away'])
const erase = await createConfirmedCustomer('throwaway', ['Throw', 'Away'])

const variables = {
  E2E_CUSTOMER_EMAIL: shared.email,
  E2E_CUSTOMER_PASSWORD: shared.password,
  E2E_THROWAWAY_EMAIL: change.email,
  E2E_THROWAWAY_PASSWORD: change.password,
  E2E_THROWAWAY_DELETE_EMAIL: erase.email,
  E2E_THROWAWAY_DELETE_PASSWORD: erase.password,
}

if (vso) {
  for (const [name, value] of Object.entries(variables)) {
    console.log(`##vso[task.setvariable variable=${name};issecret=true]${value}`)
  }
  console.log(
    'E2E accounts provisioned: 6 secret variables set for the next steps (values not shown).',
  )
} else {
  writeFileSync(
    outFile,
    Object.entries(variables)
      .map(([name, value]) => `export ${name}='${value}'`)
      .concat('')
      .join('\n'),
    { mode: 0o600 },
  )
  console.log(`E2E accounts provisioned. Variables written to ${outFile} (not printed).`)
}
