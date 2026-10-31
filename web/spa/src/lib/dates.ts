// The API sends UTC timestamps (ISO 8601 with Z). Show them in the visitor's own time zone and locale.
const formatter = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })

// The generated types say `unknown` for date-time fields, so accept anything and show '' for a bad value.
export function parseDate(value: unknown): Date | undefined {
  if (typeof value !== 'string') return undefined
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? undefined : date
}

export function formatDateTime(value: unknown): string {
  const date = parseDate(value)
  return date ? formatter.format(date) : ''
}

const DAY = /^(\d{4})-(\d{2})-(\d{2})$/

// "2026-01-10" (a day the user picked) -> the instant that day starts in the user's time zone, as ISO
// with Z, plus an optional number of days. The API takes dates with an offset (catalog 1.3);
// To is exclusive there, so "To 10 Jan" is sent as the start of 11 Jan.
export function localDayStartIso(day: string, addDays = 0): string | undefined {
  const match = DAY.exec(day)
  if (!match) return undefined
  const [year, month, date] = [Number(match[1]), Number(match[2]), Number(match[3])]
  const start = new Date(year, month - 1, date + addDays)
  // Reject 2026-02-31 style days that JavaScript would roll over.
  if (addDays === 0 && (start.getMonth() !== month - 1 || start.getDate() !== date))
    return undefined
  return Number.isNaN(start.getTime()) ? undefined : start.toISOString()
}

export function isValidDay(day: string): boolean {
  return localDayStartIso(day) !== undefined
}
