import { useQuery, type QueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useLocation, useParams, type LoaderFunctionArgs } from 'react-router'
import { QuantityInput } from '@/components/quantity-input'
import { Badge } from '@/components/ui/badge'
import { Button, buttonVariants } from '@/components/ui/button'
import { productQuery, type Product } from '@/api/products'
import { apiErrorMessage } from '@/lib/error-message'
import { addMessage, useCart } from '@/hooks/use-cart'
import { MAX_QUANTITY } from '@/lib/cart-storage'
import { ApiError } from '@/lib/problem-details'
import { formatMoney } from '@/lib/money'

export function productDetailLoader(queryClient: QueryClient) {
  return ({ params }: LoaderFunctionArgs) => {
    if (params.productId) queryClient.query(productQuery(params.productId)).catch(() => undefined)
    return null
  }
}

// The list the visitor came from ("?search=...&page=2"), so "Back to products" keeps it.
function backLink(state: unknown): string {
  if (typeof state === 'object' && state !== null && 'list' in state) {
    const list = state.list
    if (typeof list === 'string' && (list === '' || list.startsWith('?'))) return `/products${list}`
  }
  return '/products'
}

// CU-2
export function ProductDetailRoute() {
  const { productId = '' } = useParams()
  const location = useLocation()
  const { data, error, isPending, isError, refetch, isFetching } = useQuery(productQuery(productId))
  const back = backLink(location.state)

  const backToProducts = (
    <Link to={back} className="text-sm underline underline-offset-4">
      Back to products
    </Link>
  )

  if (isPending) {
    return (
      <section className="grid gap-4">
        <title>Product · Orders Shop</title>
        {backToProducts}
        <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
          Product
        </h1>
        <p role="status">Loading product…</p>
      </section>
    )
  }

  if (isError) {
    const notFound = error instanceof ApiError && error.status === 404
    return (
      <section className="grid max-w-prose justify-items-start gap-4">
        <title>{notFound ? 'Product not found' : 'Product'} · Orders Shop</title>
        <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
          {notFound ? 'Product not found' : 'We could not load this product'}
        </h1>
        {notFound ? (
          <p>Product not found. It may have been removed.</p>
        ) : (
          <div role="alert" className="rounded-md border border-destructive p-3">
            <p>{apiErrorMessage(error)}</p>
          </div>
        )}
        <div className="flex gap-2">
          {!notFound && (
            <Button
              variant="outline"
              disabled={isFetching}
              onClick={() => {
                void refetch()
              }}
            >
              {isFetching ? 'Trying again…' : 'Try again'}
            </Button>
          )}
          <Link to={back} className={buttonVariants({ variant: notFound ? 'default' : 'outline' })}>
            Back to products
          </Link>
        </div>
      </section>
    )
  }

  return (
    <section className="grid max-w-prose gap-4">
      <title>{data.name} · Orders Shop</title>
      {backToProducts}
      <Detail product={data} />
    </section>
  )
}

function Detail({ product }: { product: Product }) {
  const cart = useCart()
  const inStock = product.stockQuantity > 0
  const max = Math.min(MAX_QUANTITY, product.stockQuantity)
  const inCart = cart.lines.find((line) => line.productId === product.id)?.quantity ?? 0
  const [quantity, setQuantity] = useState(1)
  const [notice, setNotice] = useState<{ ok: boolean; text: string } | undefined>()

  function add() {
    const result = cart.add(product, quantity)
    const count = inCart > 0 ? cart.count : cart.count + 1
    setNotice({ ok: result === 'added', text: addMessage(result, product.name, count) })
  }

  return (
    <>
      <h1 tabIndex={-1} className="text-2xl font-semibold outline-none">
        {product.name}
      </h1>
      <p className="text-sm text-muted-foreground">SKU {product.sku}</p>
      <p className="text-xl font-semibold">{formatMoney(product.price)}</p>
      <div>
        <Badge variant={inStock ? 'default' : 'muted'}>
          {inStock ? 'In stock' : 'Out of stock'}
        </Badge>
      </div>
      <p className="whitespace-pre-line">{product.description ?? 'No description'}</p>

      {inStock ? (
        <>
          <QuantityInput label="Quantity" value={quantity} max={max} onChange={setQuantity} />
          {inCart > 0 && <p className="text-sm">{inCart} in cart</p>}
          <div>
            <Button type="button" onClick={add}>
              Add to cart
            </Button>
          </div>
        </>
      ) : (
        <div>
          <Button type="button" disabled>
            Out of stock
          </Button>
        </div>
      )}

      <p role="status" className="min-h-5 text-sm">
        {notice?.ok ? notice.text : ''}
      </p>
      {notice && !notice.ok && (
        <p role="alert" className="rounded-md border border-destructive p-3 text-sm">
          {notice.text}
        </p>
      )}
      {inCart > 0 && (
        <p>
          <Link to="/cart" className="text-sm underline underline-offset-4">
            View cart
          </Link>
        </p>
      )}
    </>
  )
}
