import { snapToTradingDays } from './dates'

/**
 * Month-end mean targets as a step line on trading days. Several month-ends can land on the same trading day
 * (a month-end before the first price snaps to the first bar); the chart needs strictly increasing times, so the
 * latest target for each day wins.
 */
export function targetLine(
  points: readonly { asOf: string; meanTarget?: number | null }[],
  tradingDays: readonly string[],
): { time: string; value: number }[] {
  const withTarget = points.filter((p) => p.meanTarget != null)
  const snapped = snapToTradingDays(
    withTarget.map((p) => p.asOf),
    tradingDays,
  )
  const byDay = new Map<string, number>()
  for (const p of [...withTarget].sort((a, b) => a.asOf.localeCompare(b.asOf))) {
    const day = snapped.get(p.asOf)
    if (day) byDay.set(day, p.meanTarget!)
  }
  return [...byDay].sort(([a], [b]) => a.localeCompare(b)).map(([time, value]) => ({ time, value }))
}

/** Ratings grouped by the trading day they show on (weekend calls move to the next session), oldest day first. */
export function ratingsByTradingDay<T extends { date: string }>(
  ratings: readonly T[],
  tradingDays: readonly string[],
): Map<string, T[]> {
  const snapped = snapToTradingDays(
    ratings.map((r) => r.date),
    tradingDays,
  )
  const byDay = new Map<string, T[]>()
  for (const r of [...ratings].sort((a, b) => a.date.localeCompare(b.date))) {
    const day = snapped.get(r.date)
    if (day) byDay.set(day, [...(byDay.get(day) ?? []), r])
  }
  return byDay
}
