/**
 * Date helpers. The API sends UTC timestamps (ISO 8601 with Z, for example `2026-01-10T08:30:00Z`).
 * We show them in the visitor's own time zone and locale.
 */
const formatter = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })

/**
 * Turns a value from the API into a `Date`, or `undefined` when it is not a valid date text.
 * The generated types say `unknown` for date-time fields, so this accepts anything.
 */
export function parseDate(value: unknown): Date | undefined {
  if (typeof value !== 'string') return undefined
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? undefined : date
}

/** Formats a timestamp for display (for example "Jan 10, 2026, 4:30 PM"). Returns '' for a bad value. */
export function formatDateTime(value: unknown): string {
  const date = parseDate(value)
  return date ? formatter.format(date) : ''
}

const DAY = /^(\d{4})-(\d{2})-(\d{2})$/

/**
 * Converts a day the user picked (`"2026-01-10"`) into the instant that day starts in the user's own
 * time zone, written as an ISO timestamp with Z, plus an optional number of days.
 *
 * The API takes dates with an offset (catalog 1.3). "To" is exclusive there, so "To 10 Jan" is sent as
 * the start of 11 Jan: call it with `addDays = 1`.
 *
 * @param day     A `YYYY-MM-DD` text.
 * @param addDays Days to add to the day before converting.
 * @returns The timestamp, or `undefined` when `day` is not a real calendar day (such as `2026-02-31`).
 */
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

/** True when the text is a real `YYYY-MM-DD` calendar day. */
export function isValidDay(day: string): boolean {
  return localDayStartIso(day) !== undefined
}
