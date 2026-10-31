import { Link } from 'react-router'
import { TextLink } from '@/components/text-link'
import { buttonVariants } from '@/components/ui/button'

// AN-4. The email is neither shown nor stored. The text is the same for new and existing emails.
export function CheckEmailRoute() {
  return (
    <section className="mx-auto grid max-w-md gap-4 rounded-lg border p-6">
      <title>Check your email · Orders Shop</title>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        Check your email
      </h1>
      <p>
        If this email can be used, we sent a link to confirm your account. The link works for 24
        hours.
      </p>
      <p className="text-sm">
        Did not get it? <TextLink to="/resend-confirmation">Resend email</TextLink>
      </p>
      <div>
        <Link to="/sign-in" className={buttonVariants({ variant: 'outline' })}>
          Back to sign in
        </Link>
      </div>
      {import.meta.env.DEV && (
        <p className="text-sm text-muted-foreground">
          Local setup: find the link with <code>docker compose logs api</code>.
        </p>
      )}
    </section>
  )
}
