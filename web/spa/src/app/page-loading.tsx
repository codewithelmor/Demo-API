/** The placeholder shown while the first session check is still running. */
/**
 * Shown by the router (`hydrateFallbackElement`) before the first route data has arrived.
 * `role="status"` makes screen readers announce the text politely.
 */
export function PageLoading() {
  return (
    <p role="status" className="p-4">
      Loading…
    </p>
  )
}
