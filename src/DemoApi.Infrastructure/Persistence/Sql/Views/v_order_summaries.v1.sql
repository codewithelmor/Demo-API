-- EP-50 (FR-100). Excludes deleted orders and lines; keeps deleted (anonymized) customers (D2).
CREATE OR REPLACE VIEW public.v_order_summaries WITH (security_invoker = true) AS
SELECT o.id            AS order_id,
       o.customer_id,
       c.first_name    AS customer_first_name,
       c.last_name     AS customer_last_name,
       c.is_deleted    AS customer_is_deleted,
       o.status,
       COALESCE(li.item_count, 0)::int  AS item_count,
       COALESCE(li.total_units, 0)::int AS total_units,
       o.total,
       o.currency_code,
       o.created_at
  FROM public.orders o
  JOIN public.customers c ON c.id = o.customer_id
  LEFT JOIN LATERAL (
        SELECT count(*) AS item_count, sum(i.quantity) AS total_units
          FROM public.order_items i
         WHERE i.order_id = o.id AND i.is_deleted = false) li ON true
 WHERE o.is_deleted = false;
