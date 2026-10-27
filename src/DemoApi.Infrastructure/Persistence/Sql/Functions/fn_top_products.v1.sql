-- EP-54 (FR-105). Same status rule as the sales report. Keeps deleted products (D2).
CREATE OR REPLACE FUNCTION public.fn_top_products(p_from timestamptz, p_to timestamptz, p_top int)
RETURNS TABLE (product_id uuid, sku varchar, name varchar, product_is_deleted boolean, units bigint, revenue numeric(14,2))
LANGUAGE sql STABLE
SET search_path = public, pg_temp
AS $$
    SELECT p.id, p.sku::varchar, p.name::varchar, p.is_deleted,
           sum(i.quantity)::bigint, sum(i.line_total)::numeric(14,2)
      FROM public.order_items i
      JOIN public.orders o ON o.id = i.order_id
      JOIN public.products p ON p.id = i.product_id
     WHERE i.is_deleted = false
       AND o.is_deleted = false
       AND o.status IN ('Confirmed', 'Shipped', 'Completed')
       AND o.created_at >= p_from AND o.created_at < p_to
     GROUP BY p.id, p.sku, p.name, p.is_deleted
     ORDER BY 5 DESC, 6 DESC, 1
     LIMIT greatest(least(p_top, 50), 1)
$$;
