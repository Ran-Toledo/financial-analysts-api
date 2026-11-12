import { describe, expect, it } from 'vitest'
import { addMonths, monthTicks, snapToTradingDays, todayIso, toUnixDay } from './dates'

describe('addMonths', () => {
  it('moves across months and years', () => {
    expect(addMonths('2026-11-11', -12)).toBe('2025-11-11')
    expect(addMonths('2026-11-11', 3)).toBe('2027-02-11')
  })

  it('clamps to the end of shorter months', () => {
    expect(addMonths('2026-03-31', -1)).toBe('2026-02-28')
    expect(addMonths('2024-03-31', -1)).toBe('2024-02-29')
  })
})

describe('todayIso and toUnixDay', () => {
  it('work in UTC', () => {
    expect(todayIso(new Date('2026-11-11T23:30:00Z'))).toBe('2026-11-11')
    expect(toUnixDay('1970-01-02')).toBe(86_400)
  })
})

describe('snapToTradingDays', () => {
  const days = ['2026-11-06', '2026-11-09', '2026-11-10'] // Fri, Mon, Tue

  it('moves weekend dates to the next session and keeps trading days', () => {
    const snapped = snapToTradingDays(['2026-11-06', '2026-11-08', '2026-11-01'], days)
    expect(snapped.get('2026-11-06')).toBe('2026-11-06')
    expect(snapped.get('2026-11-08')).toBe('2026-11-09')
    expect(snapped.get('2026-11-01')).toBe('2026-11-06')
  })

  it('drops dates after the last session', () => {
    expect(snapToTradingDays(['2026-11-11'], days).has('2026-11-11')).toBe(false)
  })
})

describe('monthTicks', () => {
  const ms = (iso: string) => Date.parse(`${iso}T00:00:00Z`)
  const iso = (t: number) => new Date(t).toISOString().slice(0, 10)

  it('picks calendar-aligned steps that fit', () => {
    // 20 months: quarterly would be 7 ticks, so half-yearly.
    expect(monthTicks(ms('2024-02-10'), ms('2025-10-20')).map(iso)).toEqual(['2024-07-01', '2025-01-01', '2025-07-01'])
    // 11 months: every other month, on Jan/Mar/May...
    expect(monthTicks(ms('2025-12-01'), ms('2026-11-11')).map(iso)).toEqual([
      '2026-01-01', '2026-03-01', '2026-05-01', '2026-07-01', '2026-09-01', '2026-11-01',
    ])
  })

  it('never repeats a tick and stays inside the range', () => {
    const ticks = monthTicks(ms('2023-11-15'), ms('2026-11-01'), 6)
    expect(new Set(ticks).size).toBe(ticks.length)
    expect(ticks.length).toBeLessThanOrEqual(7)
    expect(ticks[0]).toBeGreaterThanOrEqual(ms('2023-11-15'))
    expect(ticks.at(-1)).toBeLessThanOrEqual(ms('2026-11-01'))
  })
})
