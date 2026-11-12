import { describe, expect, it } from 'vitest'
import { rangePositions, splitPercents } from './consensus'

describe('splitPercents', () => {
  it('always adds up to 100', () => {
    expect(splitPercents({ buy: 1, hold: 1, sell: 1 })).toEqual({ buy: 34, hold: 33, sell: 33 })
    expect(splitPercents({ buy: 10, hold: 6, sell: 2 })).toEqual({ buy: 56, hold: 33, sell: 11 })
    expect(splitPercents({ buy: 2, hold: 0, sell: 0 })).toEqual({ buy: 100, hold: 0, sell: 0 })
  })

  it('is all zero with no calls', () => {
    expect(splitPercents({ buy: 0, hold: 0, sell: 0 })).toEqual({ buy: 0, hold: 0, sell: 0 })
  })
})

describe('rangePositions', () => {
  it('places values on a padded shared axis', () => {
    const pos = rangePositions({ low: 100, high: 200, close: 150 })
    expect(pos.low).toBeCloseTo(100 / 22)
    expect(pos.high).toBeCloseTo(100 - 100 / 22)
    expect(pos.close).toBeCloseTo(50)
  })

  it('copes with every value being equal', () => {
    const pos = rangePositions({ low: 50, high: 50 })
    expect(pos.low).toBeCloseTo(50)
    expect(pos.high).toBeCloseTo(50)
  })
})
