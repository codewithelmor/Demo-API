-- EP-52 (FR-102). UTC buckets; empty periods return zeros; edge buckets cut to [p_from, p_to).
CREATE OR REPLACE FUNCTION public.fn_sales_report(p_from timestamptz, p_to timestamptz, p_group_by text)
RETURNS TABLE (period_start timestamptz, period_end timestamptz, order_count bigint, units bigint, revenue numeric(14,2))
LANGUAGE plpgsql STABLE
SET search_path = public, pg_temp
SET "TimeZone" = 'UTC'
AS $$
#variable_conflict use_column
BEGIN
    IF p_group_by IS NULL OR p_group_by NOT IN ('day', 'week', 'month') THEN
        RAISE EXCEPTION USING ERRCODE = '22023', MESSAGE = 'invalid group_by';
    END IF;
    IF p_from IS NULL OR p_to IS NULL OR p_from >= p_to OR p_to - p_from > interval '366 days' THEN
        RAISE EXCEPTION USING ERRCODE = '22023', MESSAGE = 'invalid range';
    END IF;

    RETURN QUERY
    WITH periods AS (
        SELECT greatest(b, p_from) AS ps,
               least(b + ('1 ' || p_group_by)::interval, p_to) AS pe
          FROM generate_series(date_trunc(p_group_by, p_from, 'UTC'),
                               p_to - interval '1 microsecond',
                               ('1 ' || p_group_by)::interval) AS b),
    orders_in AS (
        SELECT o.id, o.total, o.created_at,
               (SELECT COALESCE(sum(i.quantity), 0) FROM public.order_items i
                 WHERE i.order_id = o.id AND i.is_deleted = false) AS line_units
          FROM public.orders o
         WHERE o.is_deleted = false
           AND o.status IN ('Confirmed', 'Shipped', 'Completed')
           AND o.created_at >= p_from AND o.created_at < p_to)
    SELECT p.ps, p.pe,
           count(oi.id)::bigint,
           COALESCE(sum(oi.line_units), 0)::bigint,
           COALESCE(sum(oi.total), 0)::numeric(14,2)
      FROM periods p
      LEFT JOIN orders_in oi ON oi.created_at >= p.ps AND oi.created_at < p.pe
     GROUP BY p.ps, p.pe
     ORDER BY p.ps;
END
$$;
