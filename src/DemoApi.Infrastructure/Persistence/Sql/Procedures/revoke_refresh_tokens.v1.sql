-- P9: the one real stored procedure. Revokes active refresh tokens of a user (optionally one family).
CREATE OR REPLACE PROCEDURE public.revoke_refresh_tokens(
    p_user_id uuid,
    p_family_id uuid,
    p_reason varchar,
    p_at timestamptz,
    INOUT p_revoked_count integer DEFAULT NULL)
LANGUAGE plpgsql
SET search_path = public, pg_temp
AS $$
BEGIN
    UPDATE public.refresh_tokens
       SET revoked_at = p_at,
           revoke_reason = p_reason
     WHERE user_id = p_user_id
       AND (p_family_id IS NULL OR family_id = p_family_id)
       AND revoked_at IS NULL;
    GET DIAGNOSTICS p_revoked_count = ROW_COUNT;
END
$$;
