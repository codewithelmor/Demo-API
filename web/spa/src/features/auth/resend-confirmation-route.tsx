import { TextLink } from '@/components/text-link'
import { ResendConfirmationForm } from '@/features/auth/resend-confirmation-form'

// AN-6
export function ResendConfirmationRoute() {
  return (
    <section className="mx-auto grid max-w-md gap-6 rounded-lg border p-6">
      <title>Resend confirmation · Orders Shop</title>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        Resend confirmation email
      </h1>
      <p className="text-sm text-muted-foreground">
        Enter the email you used to create your account.
      </p>
      <ResendConfirmationForm submitLabel="Send confirmation email" />
      <p>
        <TextLink to="/sign-in">Back to sign in</TextLink>
      </p>
    </section>
  )
}
