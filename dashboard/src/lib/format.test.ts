import { describe, expect, it } from 'vitest'
import { formatCount, formatMonth, formatPercent, formatScore, formatSignedPercent } from './format'

describe('format', () => {
  it('formats fractions as one-decimal percents', () => {
    expect(formatPercent(0.6667)).toBe('66.7%')
    expect(formatPercent(1)).toBe('100.0%')
    expect(formatPercent(null)).toBe('—')
  })

  it('signs percents and never shows a negative zero', () => {
    expect(formatSignedPercent(0.1509)).toBe('+15.1%')
    expect(formatSignedPercent(-0.02)).toBe('−2.0%')
    expect(formatSignedPercent(-0.0004)).toBe('0.0%')
    expect(formatSignedPercent(undefined)).toBe('—')
  })

  it('formats scores, counts and months', () => {
    expect(formatScore(72.25)).toBe('72.3')
    expect(formatScore(50)).toBe('50.0')
    expect(formatCount(1284)).toBe('1,284')
    expect(formatMonth('2026-10-31')).toBe('Oct 2026')
    expect(formatMonth('2026-01-01')).toBe('Jan 2026')   // no time-zone slip into December
  })
})
