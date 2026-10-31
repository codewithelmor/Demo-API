import { HttpResponse } from 'msw'

// The 202 AcceptedResponse of register, resend and forgot (same body for every outcome).
export function accepted() {
  return HttpResponse.json({ message: 'Accepted' }, { status: 202 })
}

// A ProblemDetails response as the BFF and API send it (generated test data only).
export function problem(
  status: number,
  slug: string,
  extra: Record<string, unknown> = {},
  headers: Record<string, string> = {},
) {
  return HttpResponse.json(
    {
      type: `/problems/${slug}`,
      title: `Problem ${slug}`,
      status,
      traceId: 'trace-test-123',
      ...extra,
    },
    { status, headers: { 'Content-Type': 'application/problem+json', ...headers } },
  )
}
