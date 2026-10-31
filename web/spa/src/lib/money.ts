/** Money formatting. The API has one configured currency (appsettings "CurrencyCode"); product responses carry none. */
const PRODUCT_CURRENCY = 'USD'

const formatter = new Intl.NumberFormat('en-US', { style: 'currency', currency: PRODUCT_CURRENCY })

/** Formats a product price, for example `12.5` becomes `$12.50`. */
export function formatMoney(amount: number): string {
  return formatter.format(amount)
}

/**
 * Formats an amount in the currency an order carries (an ISO 4217 code such as `USD` or `EUR`).
 * A code the browser does not know falls back to plain text like `12.50 XYZ`.
 */
export function formatMoneyIn(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(amount)
  } catch {
    return `${amount.toFixed(2)} ${currency}`
  }
}
