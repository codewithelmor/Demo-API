import { useMutation } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { signOut } from '@/lib/session'
import { useEndSession } from '@/hooks/use-end-session'

export function SignOutButton() {
  const endSession = useEndSession()
  const mutation = useMutation({
    mutationFn: signOut,
    // The BFF removes the session even when the API call fails (US-F004 AC2), so the SPA
    // leaves the signed-in state whatever the answer was. The cart is cleared on Sign out
    // (UX-D2, NFR-F41), but not when a session just ends.
    onSettled: () => endSession('signed-out', { clearCart: true }),
  })

  return (
    <Button
      variant="outline"
      size="sm"
      disabled={mutation.isPending}
      aria-busy={mutation.isPending}
      onClick={() => {
        mutation.mutate()
      }}
    >
      {mutation.isPending ? 'Signing out…' : 'Sign out'}
    </Button>
  )
}
