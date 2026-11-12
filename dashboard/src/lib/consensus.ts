export type Category = 'Buy' | 'Hold' | 'Sell'

export const CATEGORIES: readonly Category[] = ['Buy', 'Hold', 'Sell']

export interface Split {
  buy: number
  hold: number
  sell: number
}

/** Whole-number percents that always add up to 100 (largest remainder), or all zero when there are no calls. */
export function splitPercents({ buy, hold, sell }: Split): Split {
  const total = buy + hold + sell
  if (total === 0) return { buy: 0, hold: 0, sell: 0 }

  const raw = [buy, hold, sell].map((n) => (n / total) * 100)
  const floors = raw.map(Math.floor)
  let left = 100 - floors.reduce((a, b) => a + b, 0)
  const byRemainder = raw.map((r, i) => ({ i, rem: r - floors[i] })).sort((a, b) => b.rem - a.rem || a.i - b.i)
  for (const { i } of byRemainder) {
    if (left-- <= 0) break
    floors[i]++
  }
  return { buy: floors[0], hold: floors[1], sell: floors[2] }
}

/**
 * Where each value sits along a shared axis, as 0–100 percents, for the target range figure.
 * The axis spans every value with 5% padding each side, so no marker sits on the edge.
 */
export function rangePositions(values: Record<string, number>): Record<string, number> {
  const all = Object.values(values)
  const min = Math.min(...all)
  const max = Math.max(...all)
  const span = max - min || Math.abs(max) || 1
  const lo = min - span * 0.05
  const hi = max + span * 0.05
  return Object.fromEntries(Object.entries(values).map(([k, v]) => [k, ((v - lo) / (hi - lo)) * 100]))
}
