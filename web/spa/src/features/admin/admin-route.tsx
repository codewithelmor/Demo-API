/**
 * SY-5: the back office page. It is a placeholder in Release 1. This module is its own chunk
 * (the router loads it with lazy()), and nothing outside features/admin imports it.
 * Sign-out is in the app shell header.
 */
export function AdminRoute() {
  return (
    <section className="grid max-w-prose gap-4">
      <title>Back office · Orders Shop</title>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        Back office coming soon
      </h1>
      <p>Admin tools arrive in the next release. Use the API documentation for now.</p>
    </section>
  )
}
