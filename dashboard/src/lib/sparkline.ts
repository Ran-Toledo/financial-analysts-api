export interface SparkPoint {
  x: number
  y: number
}

export interface SparkGeometry {
  points: SparkPoint[]
  path: string
}

/**
 * Lays values out across a width × height box (y grows downward), with `pad` pixels kept clear on every side
 * so the end dot isn't clipped. The y-range spans at least `minSpan` so a flat series isn't blown up into noise.
 */
export function sparklineGeometry(
  values: readonly number[],
  width: number,
  height: number,
  { pad = 3, minSpan = 10 }: { pad?: number; minSpan?: number } = {},
): SparkGeometry | null {
  if (values.length < 2) return null

  let min = Math.min(...values)
  let max = Math.max(...values)
  if (max - min < minSpan) {
    const mid = (min + max) / 2
    min = mid - minSpan / 2
    max = mid + minSpan / 2
  }

  const innerW = width - 2 * pad
  const innerH = height - 2 * pad
  const points = values.map((v, i) => ({
    x: round(pad + (i / (values.length - 1)) * innerW),
    y: round(pad + (1 - (v - min) / (max - min)) * innerH),
  }))
  const path = points.map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x} ${p.y}`).join(' ')
  return { points, path }
}

/** Index of the point closest to `x` horizontally. */
export function nearestIndex(points: readonly SparkPoint[], x: number): number {
  let best = 0
  for (let i = 1; i < points.length; i++) {
    if (Math.abs(points[i].x - x) < Math.abs(points[best].x - x)) best = i
  }
  return best
}

function round(n: number): number {
  return Math.round(n * 100) / 100
}
