import { Link } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import type { Product } from '@/api/products'
import { formatMoney } from '@/lib/money'

/** Props for {@link ProductCard}. */
interface ProductCardProps {
  /** The product to show. */
  product: Product
  /** How many of this product are in the cart (0 = none). */
  inCart: number
  /** The list's query string, so the detail page can link back to the same list. */
  listSearch: string
  /** Called when the user presses "Add to cart". */
  onAdd: (product: Product) => void
}

/** One product in the catalog grid: name, price, stock state, a link to the detail page and an add button. */
export function ProductCard({ product, inCart, listSearch, onAdd }: ProductCardProps) {
  const inStock = product.stockQuantity > 0
  const label = inCart > 0 ? 'Add one more' : 'Add to cart'
  return (
    <li className="grid content-start gap-2 rounded-lg border p-4">
      <h2 className="text-lg font-medium">
        <Link
          to={`/products/${product.id}`}
          state={{ list: listSearch }}
          className="underline-offset-4 hover:underline"
        >
          {product.name}
        </Link>
      </h2>
      <p className="text-sm text-muted-foreground">SKU {product.sku}</p>
      <p className="font-semibold">{formatMoney(product.price)}</p>
      <div>
        <Badge variant={inStock ? 'default' : 'muted'}>
          {inStock ? 'In stock' : 'Out of stock'}
        </Badge>
      </div>
      {inCart > 0 && <p className="text-sm">{inCart} in cart</p>}
      <div>
        {inStock ? (
          <Button
            type="button"
            variant="outline"
            aria-label={`${label}: ${product.name}`}
            onClick={() => {
              onAdd(product)
            }}
          >
            {label}
          </Button>
        ) : (
          <Button type="button" variant="outline" disabled>
            Out of stock
          </Button>
        )}
      </div>
    </li>
  )
}
