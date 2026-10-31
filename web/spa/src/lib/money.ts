// The API has one configured currency (appsettings "CurrencyCode"); product responses carry none.
const PRODUCT_CURRENCY = 'USD'

const formatter = new Intl.NumberFormat('en-US', { style: 'currency', currency: PRODUCT_CURRENCY })

export function formatMoney(amount: number): string {
  return formatter.format(amount)
}

// An order carries its own currency code (ISO 4217). A code the browser does not know falls back to the code text.
export function formatMoneyIn(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(amount)
  } catch {
    return `${amount.toFixed(2)} ${currency}`
  }
}
