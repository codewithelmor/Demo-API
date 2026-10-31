/**
 * Entry point of the web app. The browser loads this file first (index.html points at it).
 *
 * It does three things:
 * 1. Applies the security nonce (see `app/csp-nonce.ts`).
 * 2. Creates the shared data cache (`QueryClient`) and the router, which maps URLs to pages.
 * 3. Renders the whole React tree into the `<div id="root">` element of index.html.
 *
 * This is a single-page app (SPA): the browser loads the page once and React swaps the screens in
 * place when the URL changes, instead of asking the server for a new HTML page each time.
 */
import '@/app/csp-nonce'
import { QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router/dom'
import { createQueryClient } from '@/app/query-client'
import { CartProvider } from '@/hooks/cart-provider'
import { createAppRouter } from '@/app/router'
import '@/styles/globals.css'

const container = document.getElementById('root')
if (!container) {
  throw new Error('Missing #root element in index.html.')
}

// One cache for all server data. The router gets it too, so route loaders can fill it before a page shows.
const queryClient = createQueryClient()
const router = createAppRouter(queryClient)

// StrictMode only affects development: it renders components twice to expose side-effect bugs early.
// QueryClientProvider makes the cache available to every component; RouterProvider shows the page for the URL.
createRoot(container).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <CartProvider>
        <RouterProvider router={router} />
      </CartProvider>
    </QueryClientProvider>
  </StrictMode>,
)
