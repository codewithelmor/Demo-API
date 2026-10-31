import { useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router'
import { clearCartFor } from '@/lib/cart-storage'
import { endSessionLocally, useSession, type SessionEndReason } from '@/lib/session'

// Leaves the signed-in state in the SPA after the BFF ended the session, and goes to sign-in with a reason.
// The cart is cleared only when asked (Sign out, Sign out everywhere, Delete account; UX-D2, NFR-F41),
// never when a session just ends (for example after a password change).
// RequireRole redirects to the same address when the session goes, so the two navigations agree.
export function useEndSession() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const customerId = useSession()?.customerId

  return async (reason: SessionEndReason, options: { clearCart: boolean }): Promise<void> => {
    if (options.clearCart) clearCartFor(customerId)
    endSessionLocally(queryClient, reason)
    await navigate(`/sign-in?reason=${reason}`, { replace: true })
  }
}
