import { useMemo, useState } from 'react'
import type { Schemas } from '../api/client'
import { formatMonth, formatScore } from '../lib/format'
import { nearestIndex, sparklineGeometry } from '../lib/sparkline'

const WIDTH = 96
const HEIGHT = 28

/** Month-end score history. Hover shows the month and score; the dot marks the latest month. */
export function Sparkline({ trend }: { trend: readonly Schemas['TrendPoint'][] }) {
  const [active, setActive] = useState<number | null>(null)
  const geometry = useMemo(() => sparklineGeometry(trend.map((t) => t.score), WIDTH, HEIGHT), [trend])

  if (!geometry) return <span className="muted" title="Not enough history yet">—</span>

  const first = trend[0]
  const last = trend[trend.length - 1]
  const label =
    `Score trend: ${formatScore(first.score)} in ${formatMonth(first.asOf)}, ` +
    `${formatScore(last.score)} in ${formatMonth(last.asOf)}`
  const dot = geometry.points[active ?? geometry.points.length - 1]
  const point = active == null ? null : trend[active]

  return (
    <span className="sparkline" onMouseLeave={() => setActive(null)}>
      <svg
        width={WIDTH}
        height={HEIGHT}
        viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
        role="img"
        aria-label={label}
        onMouseMove={(e) => {
          const box = e.currentTarget.getBoundingClientRect()
          setActive(nearestIndex(geometry.points, ((e.clientX - box.left) / box.width) * WIDTH))
        }}
      >
        {active != null && <line className="sparkline-crosshair" x1={dot.x} x2={dot.x} y1={0} y2={HEIGHT} />}
        <path className="sparkline-line" d={geometry.path} />
        <circle className="sparkline-dot" cx={dot.x} cy={dot.y} r={3.5} />
      </svg>
      {point && (
        <span className="sparkline-tip" style={{ left: dot.x }} role="status">
          <span className="muted">{formatMonth(point.asOf)}</span> <strong>{formatScore(point.score)}</strong>
        </span>
      )}
    </span>
  )
}
