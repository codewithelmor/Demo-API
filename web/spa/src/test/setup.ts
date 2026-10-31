import '@testing-library/jest-dom/vitest'
import { cleanup, configure } from '@testing-library/react'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { server } from '@/test/server'

// The first test in a file pays for loading the lazy route chunks (cold module transform).
configure({ asyncUtilTimeout: 5000 })

// Fail on any request without a handler, so tests never call a real API by accident.
// MSW 3 renamed onUnhandledRequest to onUnhandledFrame (it also covers WebSocket frames).
beforeAll(() => {
  server.listen({ onUnhandledFrame: 'error' })
})
afterEach(() => {
  cleanup()
  server.resetHandlers()
  window.localStorage.clear()
})
afterAll(() => {
  server.close()
})
