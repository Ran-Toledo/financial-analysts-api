import { describe, expect, it } from 'vitest'
import { nearestIndex, sparklineGeometry } from './sparkline'

describe('sparklineGeometry', () => {
  it('needs at least two points', () => {
    expect(sparklineGeometry([], 100, 20)).toBeNull()
    expect(sparklineGeometry([50], 100, 20)).toBeNull()
  })

  it('spans the padded box, with higher scores higher up', () => {
    const g = sparklineGeometry([40, 80, 60], 100, 26, { pad: 3 })!
    expect(g.points).toEqual([
      { x: 3, y: 23 },
      { x: 50, y: 3 },
      { x: 97, y: 13 },
    ])
    expect(g.path).toBe('M3 23 L50 3 L97 13')
  })

  it("doesn't blow a flat line up into noise", () => {
    const g = sparklineGeometry([60, 60.4, 60], 100, 26, { pad: 3, minSpan: 10 })!
    for (const p of g.points) expect(p.y).toBeGreaterThan(12)
    for (const p of g.points) expect(p.y).toBeLessThan(14)
  })
})

describe('nearestIndex', () => {
  it('finds the closest point horizontally', () => {
    const points = [{ x: 0, y: 0 }, { x: 50, y: 0 }, { x: 100, y: 0 }]
    expect(nearestIndex(points, -5)).toBe(0)
    expect(nearestIndex(points, 30)).toBe(1)
    expect(nearestIndex(points, 80)).toBe(2)
  })
})
