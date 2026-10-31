import { z } from 'zod'

// The ONLY place that imports 'zod' for runtime use; every other file imports `z` from '@/lib/zod'.
// Zod probes `new Function('')` when an object schema is created, and our CSP (script-src 'self', no
// unsafe-eval) reports that caught probe as a securitypolicyviolation. A module-level schema is created while
// its module loads, and the bundler may put that module in the same chunk as Zod, so a config call in main.tsx
// would run too late (this happened). Importing through this module guarantees the config runs before any
// schema exists. The cost is the JIT parser, which is a speed-up we do not need for these tiny forms.
z.config({ jitless: true })

export { z }
