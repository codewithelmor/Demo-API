import { queryOptions } from '@tanstack/react-query'
import { bffVoid } from '@/api/bff'
import { api, expectOk, unwrapWithResponse } from '@/api/client'
import type { components } from '@/api/schema'

export type Profile = components['schemas']['MyProfileResponse']

// The profile and the ETag that came with it. The ETag is opaque: it is only sent back as If-Match (NFR-F22).
export interface ProfileData {
  profile: Profile
  etag: string | undefined
}

export const profileKey = ['profile'] as const

function withEtag(result: { data: Profile; response: Response }): ProfileData {
  return { profile: result.data, etag: result.response.headers.get('ETag') ?? undefined }
}

// GET /api/v1/customers/me (EP-35)
export const profileQuery = queryOptions({
  queryKey: profileKey,
  queryFn: async ({ signal }): Promise<ProfileData> =>
    withEtag(await unwrapWithResponse<Profile>(api.GET('/api/v1/customers/me', { signal }))),
})

export interface ProfileInput {
  firstName: string
  lastName: string
  // Empty clears the phone: the API replaces the whole record, so null removes it.
  phone: string
}

// PUT /api/v1/customers/me (EP-36). Needs If-Match: 428 when missing, 412 when stale.
export async function updateProfile(etag: string | undefined, input: ProfileInput) {
  return withEtag(
    await unwrapWithResponse<Profile>(
      api.PUT('/api/v1/customers/me', {
        params: { header: etag === undefined ? {} : { 'If-Match': etag } },
        body: {
          firstName: input.firstName,
          lastName: input.lastName,
          phone: input.phone === '' ? null : input.phone,
        },
      }),
    ),
  )
}

// POST /api/v1/auth/change-password (EP-09). 204; the BFF ends the session on success.
export function changePassword(body: {
  currentPassword: string
  newPassword: string
}): Promise<void> {
  return expectOk(api.POST('/api/v1/auth/change-password', { body }))
}

// POST /api/v1/customers/me/erase (EP-37). 204; the BFF ends the session on success.
export function eraseAccount(currentPassword: string): Promise<void> {
  return expectOk(api.POST('/api/v1/customers/me/erase', { body: { currentPassword } }))
}

// POST /bff/logout-all (EP-07 through the BFF, which also deletes this session).
export function signOutEverywhere(): Promise<void> {
  return bffVoid('/bff/logout-all', { method: 'POST' })
}
