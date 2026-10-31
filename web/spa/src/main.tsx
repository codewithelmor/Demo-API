import '@/app/csp-nonce'
import { QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router/dom'
import { createQueryClient } from '@/app/query-client'
import { createAppRouter } from '@/app/router'
import '@/styles/globals.css'

const container = document.getElementById('root')
if (!container) {
  throw new Error('Missing #root element in index.html.')
}

const queryClient = createQueryClient()
const router = createAppRouter(queryClient)

createRoot(container).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
)
