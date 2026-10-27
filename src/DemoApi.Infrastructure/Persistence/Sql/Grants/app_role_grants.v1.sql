-- Least privilege for the API role (data-model section 5, ADR-0006). Skipped when the role does not exist
-- (plain local databases created without deploy/compose/postgres/01-roles.sh).
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'demoapi_app') THEN
        RAISE NOTICE 'demoapi_app does not exist; grants skipped';
        RETURN;
    END IF;

    REVOKE ALL ON SCHEMA public FROM PUBLIC;
    GRANT USAGE ON SCHEMA public TO demoapi_app;
    REVOKE ALL ON ALL TABLES IN SCHEMA public FROM demoapi_app;
    REVOKE EXECUTE ON ALL ROUTINES IN SCHEMA public FROM PUBLIC;

    GRANT SELECT, INSERT, UPDATE ON public.products, public.customers, public.orders, public.order_items,
        public.order_status_history, public.refresh_tokens, public.asp_net_users TO demoapi_app;
    GRANT SELECT ON public.asp_net_roles, public.asp_net_user_claims, public.asp_net_role_claims,
        public.asp_net_user_logins, public.asp_net_user_tokens TO demoapi_app;
    -- The only DELETE in the database: Identity removes role rows (FR-035). Every removal is audited.
    GRANT SELECT, INSERT, DELETE ON public.asp_net_user_roles TO demoapi_app;
    -- Append-only audit log.
    GRANT SELECT, INSERT ON public.audit_logs TO demoapi_app;
    GRANT SELECT ON public.v_order_summaries TO demoapi_app;
    GRANT EXECUTE ON FUNCTION public.fn_customer_order_history(uuid, int, int) TO demoapi_app;
    GRANT EXECUTE ON FUNCTION public.fn_sales_report(timestamptz, timestamptz, text) TO demoapi_app;
    GRANT EXECUTE ON FUNCTION public.fn_top_products(timestamptz, timestamptz, int) TO demoapi_app;
    GRANT EXECUTE ON PROCEDURE public.revoke_refresh_tokens(uuid, uuid, varchar, timestamptz, integer) TO demoapi_app;
END
$$;
