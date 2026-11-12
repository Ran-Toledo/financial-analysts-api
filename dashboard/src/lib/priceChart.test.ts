import { describe, expect, it } from 'vitest'
import { ratingsByTradingDay, targetLine } from './priceChart'

const days = ['2025-11-11', '2025-11-12', '2025-12-01', '2026-01-02']

describe('targetLine', () => {
  it('keeps one point per trading day, the latest month-end winning', () => {
    // Sep 30 and Oct 31 both fall before the first bar and snap onto it.
    const line = targetLine(
      [
        { asOf: '2025-10-31', meanTarget: 210 },
        { asOf: '2025-09-30', meanTarget: 200 },
        { asOf: '2025-11-30', meanTarget: 220 },
        { asOf: '2025-12-31', meanTarget: null },
        { asOf: '2026-11-11', meanTarget: 250 }, // after the last bar: dropped
      ],
      days,
    )
    expect(line).toEqual([
      { time: '2025-11-11', value: 210 },
      { time: '2025-12-01', value: 220 },
    ])
  })

  it('is empty without targets', () => {
    expect(targetLine([{ asOf: '2025-11-30', meanTarget: null }], days)).toEqual([])
  })
})

describe('ratingsByTradingDay', () => {
  it('groups calls on the session they show on, oldest first', () => {
    const grouped = ratingsByTradingDay(
      [
        { id: 3, date: '2025-11-30' },
        { id: 1, date: '2025-11-11' },
        { id: 2, date: '2025-11-29' },
      ],
      days,
    )
    expect([...grouped.keys()]).toEqual(['2025-11-11', '2025-12-01'])
    expect(grouped.get('2025-12-01')!.map((r) => r.id)).toEqual([2, 3])
  })
})
