-- EP-51 (FR-101). Always returns at least one row so total_count is right on empty pages.
CREATE OR REPLACE FUNCTION public.fn_customer_order_history(p_customer_id uuid, p_page int, p_page_size int)
RETURNS TABLE (total_count bigint, order_id uuid, status varchar, item_count int, total_units int,
               total numeric(12,2), currency_code char(3), created_at timestamptz)
LANGUAGE plpgsql STABLE
SET search_path = public, pg_temp
AS $$
#variable_conflict use_column
BEGIN
    IF p_page IS NULL OR p_page < 1 OR p_page_size IS NULL OR p_page_size NOT BETWEEN 1 AND 100 THEN
        RAISE EXCEPTION USING ERRCODE = '22023', MESSAGE = 'invalid paging';
    END IF;

    RETURN QUERY
    WITH t AS (
        SELECT count(*) AS c
          FROM public.orders o
         WHERE o.customer_id = p_customer_id AND o.is_deleted = false)
    SELECT t.c, pg.id, pg.status::varchar, pg.item_count, pg.total_units, pg.total, pg.currency_code, pg.created_at
      FROM t
      LEFT JOIN LATERAL (
            SELECT o.id, o.status,
                   (SELECT count(*)::int FROM public.order_items i WHERE i.order_id = o.id AND i.is_deleted = false) AS item_count,
                   (SELECT COALESCE(sum(i.quantity), 0)::int FROM public.order_items i WHERE i.order_id = o.id AND i.is_deleted = false) AS total_units,
                   o.total, o.currency_code, o.created_at
              FROM public.orders o
             WHERE o.customer_id = p_customer_id AND o.is_deleted = false
             ORDER BY o.created_at DESC, o.id DESC
             LIMIT p_page_size OFFSET (p_page - 1) * p_page_size) pg ON true;
END
$$;
