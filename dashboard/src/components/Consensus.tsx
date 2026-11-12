import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { Schemas } from '../api/client'
import { rangePositions, splitPercents } from '../lib/consensus'
import { formatDate, formatMonth, formatPrice } from '../lib/format'
import { Legend } from './Controls'
import { CategorySwatch, ChartTip } from './ProfileCharts'

/** Today's Buy/Hold/Sell split as one thin segmented bar, with counts beside it, plus the price-target summary. */
export function ConsensusPanel({ consensus }: { consensus: Schemas['ConsensusDto'] }) {
  const c = consensus
  const pct = splitPercents(c)

  if (c.contributors === 0) {
    return <p className="state-body">No calls in the last {c.lookbackDays} days.</p>
  }

  return (
    <div className="consensus">
      <p className="consensus-head">
        <span className="consensus-rating">{c.rating}</span>
        <span className="muted">
          {' '}
          consensus · score {c.score?.toFixed(2)} on a 1 (all Buy) to 3 (all Sell) scale
        </span>
      </p>

      <div className="split" role="img" aria-label={`${c.buy} Buy, ${c.hold} Hold, ${c.sell} Sell`}>
        {(['buy', 'hold', 'sell'] as const).map((k) =>
          c[k] > 0 ? <span key={k} className={`split-part dot-${cap(k)}`} style={{ flexGrow: c[k] }} /> : null,
        )}
      </div>
      <ul className="split-legend">
        {(['buy', 'hold', 'sell'] as const).map((k) => (
          <li key={k}>
            <CategorySwatch category={cap(k)} />
            <strong>{c[k]}</strong> {cap(k)} <span className="muted">{pct[k]}%</span>
          </li>
        ))}
      </ul>
      <p className="chart-note">
        Latest call from each of {c.contributors} contributors in the {c.lookbackDays} days to {formatDate(c.asOf)}.
      </p>

      <h3>Price targets</h3>
      {c.targets ? <TargetRange targets={c.targets} lastClose={c.lastClose} /> : (
        <p className="chart-note">No price targets in this window.</p>
      )}
    </div>
  )
}

function TargetRange({ targets, lastClose }: { targets: Schemas['TargetSummaryDto']; lastClose: number | null | undefined }) {
  const values: Record<string, number> = { low: targets.low, high: targets.high, mean: targets.mean }
  if (lastClose != null) values.close = lastClose
  const pos = rangePositions(values)
  const upside = targets.upsidePct

  return (
    <>
      <p className="target-summary">
        Mean <strong>{formatPrice(targets.mean)}</strong>
        {upside != null && (
          <>
            , {Math.abs(upside).toFixed(1)}% {upside >= 0 ? 'above' : 'below'} the last close
          </>
        )}
        <span className="muted"> · {targets.count} targets</span>
      </p>
      <div className="range" aria-hidden="true">
        <span className="range-track" style={{ left: `${pos.low}%`, width: `${pos.high - pos.low}%` }} />
        <span className="range-mark range-mean" style={{ left: `${pos.mean}%` }} />
        {pos.close != null && <span className="range-mark range-close" style={{ left: `${pos.close}%` }} />}
      </div>
      <div className="range-labels">
        <span>Low {formatPrice(targets.low)}</span>
        <span>High {formatPrice(targets.high)}</span>
      </div>
      <Legend
        items={[
          { label: 'Mean target', swatch: <span className="swatch-tick swatch-target-tick" /> },
          ...(lastClose != null
            ? [{ label: `Last close ${formatPrice(lastClose)}`, swatch: <span className="swatch-tick swatch-close-tick" /> }]
            : []),
        ]}
      />
    </>
  )
}

/** Month-end Buy/Hold/Sell split as 100% stacked columns. */
export function ConsensusHistoryChart({ points }: { points: readonly Schemas['ConsensusPointDto'][] }) {
  const data = points.filter((p) => p.contributors > 0)
  return (
    <>
      <ResponsiveContainer width="100%" height={220}>
        <BarChart data={data} stackOffset="expand" margin={{ top: 8, right: 12, bottom: 0, left: 0 }} barCategoryGap="20%">
          <CartesianGrid vertical={false} stroke="var(--grid)" />
          <XAxis
            dataKey="asOf"
            stroke="var(--border)"
            tick={{ fill: 'var(--text-3)', fontSize: 12 }}
            tickLine={false}
            tickFormatter={(d: string) => formatMonth(d).split(' ')[0]}
            interval="preserveStartEnd"
          />
          <YAxis
            stroke="var(--border)"
            tick={{ fill: 'var(--text-3)', fontSize: 12 }}
            tickLine={false}
            axisLine={false}
            width={48}
            tickFormatter={(v: number) => `${Math.round(v * 100)}%`}
          />
          <Tooltip
            cursor={{ fill: 'var(--surface-2)' }}
            content={(props) => {
              const p = props.active ? (props.payload?.[0]?.payload as Schemas['ConsensusPointDto'] | undefined) : undefined
              if (!p) return null
              const pct = splitPercents(p)
              return (
                <ChartTip>
                  <div className="muted">{formatMonth(p.asOf)}</div>
                  <div>
                    {p.buy} Buy ({pct.buy}%) · {p.hold} Hold ({pct.hold}%) · {p.sell} Sell ({pct.sell}%)
                  </div>
                  {p.meanTarget != null && <div>Mean target {formatPrice(p.meanTarget)}</div>}
                </ChartTip>
              )
            }}
          />
          {/* Sell at the bottom, Buy on top, with a 2px surface gap between segments. */}
          <Bar dataKey="sell" stackId="s" fill="var(--sell)" stroke="var(--surface)" strokeWidth={2} isAnimationActive={false} />
          <Bar dataKey="hold" stackId="s" fill="var(--hold)" stroke="var(--surface)" strokeWidth={2} isAnimationActive={false} />
          <Bar
            dataKey="buy"
            stackId="s"
            fill="var(--buy)"
            stroke="var(--surface)"
            strokeWidth={2}
            radius={[4, 4, 0, 0]}
            isAnimationActive={false}
          />
        </BarChart>
      </ResponsiveContainer>
      <Legend
        items={(['Buy', 'Hold', 'Sell'] as const).map((c) => ({ label: c, swatch: <CategorySwatch category={c} /> }))}
      />
    </>
  )
}

function cap<T extends string>(s: T): Capitalize<T> {
  return (s.charAt(0).toUpperCase() + s.slice(1)) as Capitalize<T>
}
