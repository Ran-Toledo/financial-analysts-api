// Dates travel as ISO "YYYY-MM-DD" strings (DateOnly in the API). Work in UTC so no time zone shifts a day.

export function todayIso(now: Date = new Date()): string {
  return now.toISOString().slice(0, 10)
}

/** "2026-11-30" plus -1 month -> "2026-10-30"; clamps to the month's last day (Mar 31 - 1 month -> Feb 28). */
export function addMonths(isoDate: string, months: number): string {
  const [y, m, d] = isoDate.split('-').map(Number)
  const target = new Date(Date.UTC(y, m - 1 + months, 1))
  const lastDay = new Date(Date.UTC(target.getUTCFullYear(), target.getUTCMonth() + 1, 0)).getUTCDate()
  target.setUTCDate(Math.min(d, lastDay))
  return target.toISOString().slice(0, 10)
}

/** Seconds since the epoch at UTC midnight, the time format the price chart uses. */
export function toUnixDay(isoDate: string): number {
  return Date.parse(`${isoDate}T00:00:00Z`) / 1000
}

/**
 * Moves each date onto the first trading day on or after it (ratings land on weekends and holidays; the chart
 * only has bars on trading days). Dates after the last trading day are dropped. `tradingDays` must be sorted.
 */
export function snapToTradingDays(dates: readonly string[], tradingDays: readonly string[]): Map<string, string> {
  const snapped = new Map<string, string>()
  for (const date of dates) {
    let lo = 0
    let hi = tradingDays.length
    while (lo < hi) {
      const mid = (lo + hi) >> 1
      if (tradingDays[mid] < date) lo = mid + 1
      else hi = mid
    }
    if (lo < tradingDays.length) snapped.set(date, tradingDays[lo])
  }
  return snapped
}

/**
 * Up to `maxTicks` month starts between two dates (as epoch ms), spaced 1, 2, 3, 6 or 12 months apart, for a time
 * axis. Ticks land on calendar boundaries (Jan/Apr/Jul/Oct for quarters), so labels read as plain months.
 */
export function monthTicks(fromMs: number, toMs: number, maxTicks = 6): number[] {
  const from = new Date(fromMs)
  const months = (new Date(toMs).getUTCFullYear() - from.getUTCFullYear()) * 12 + new Date(toMs).getUTCMonth() - from.getUTCMonth()
  const step = [1, 2, 3, 6, 12, 24].find((s) => months / s < maxTicks) ?? 24

  const ticks: number[] = []
  const firstMonth = Math.ceil((from.getUTCFullYear() * 12 + from.getUTCMonth() + (from.getUTCDate() > 1 ? 1 : 0)) / step) * step
  for (let m = firstMonth; ; m += step) {
    const t = Date.UTC(Math.floor(m / 12), m % 12, 1)
    if (t > toMs) break
    ticks.push(t)
  }
  return ticks
}
