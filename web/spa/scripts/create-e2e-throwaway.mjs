// Creates TWO new confirmed Customers for the throw-away e2e journeys (one changes its password, the other is
// erased, so the two tests do not depend on each other) and writes the E2E_THROWAWAY_* and E2E_THROWAWAY_DELETE_* variables to a shell file OUTSIDE the repository. Run it once before every e2e run:
//
//   node scripts/create-e2e-throwaway.mjs            # file: $TEMP/e2e-throwaway-env.sh (or $TMPDIR, or /tmp)
//   source "$TEMP/e2e-throwaway-env.sh"              # exports the four variables
//
// How: it registers through the API (default http://localhost:8080, the compose-published port), then takes the
// confirmation link from the API log (the dev stack uses a fake e-mail sender that logs the link) and confirms.
// Nothing secret is printed: the password is random, lives only in that file, and the tests erase the account.
// The API log line contains a one-time confirmation token, so this script is for the local compose stack only.
// The pipeline uses provision-e2e-ci.mjs instead (shared Customer, a product, secret pipeline variables).
import { writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { createConfirmedCustomer } from './e2e-accounts.mjs'

const outFile =
  process.argv[2] ??
  join(process.env.TEMP ?? process.env.TMPDIR ?? tmpdir(), 'e2e-throwaway-env.sh')

const change = await createConfirmedCustomer('throwaway', ['Throw', 'Away'])
const erase = await createConfirmedCustomer('throwaway', ['Throw', 'Away'])
writeFileSync(
  outFile,
  [
    `export E2E_THROWAWAY_EMAIL='${change.email}'`,
    `export E2E_THROWAWAY_PASSWORD='${change.password}'`,
    `export E2E_THROWAWAY_DELETE_EMAIL='${erase.email}'`,
    `export E2E_THROWAWAY_DELETE_PASSWORD='${erase.password}'`,
    '',
  ].join('\n'),
  { mode: 0o600 },
)
console.log(
  `Two throw-away accounts created and confirmed. Variables written to ${outFile} (not printed).`,
)
