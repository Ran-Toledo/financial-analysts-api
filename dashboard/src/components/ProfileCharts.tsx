import type { ReactNode } from 'react'
import {
  CartesianGrid,
  Line,
  LineChart,
  ReferenceLine,
  ResponsiveContainer,
  Scatter,
  ScatterChart,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import type { Schemas } from '../api/client'
import { CATEGORIES, type Category } from '../lib/consensus'
import { monthTicks } from '../lib/dates'
import { formatDate, formatMonth, formatScore, formatSignedPercent } from '../lib/format'
import { Legend } from './Controls'

const AXIS = { stroke: 'var(--border)', tick: { fill: 'var(--text-3)', fontSize: 12 }, tickLine: false } as const

/** Recharts hands tooltips the hovered datum as payload[0].payload. */
function hovered<T>(props: { active?: boolean; payload?: readonly { payload?: unknown }[] }): T | null {
  return props.active && props.payload?.length ? (props.payload[0].payload as T) : null
}

export function ChartTip({ children }: { children: ReactNode }) {
  return <div className="chart-tip">{children}</div>
}

// ---- Score trend ----

export function ScoreTrendChart({ trend }: { trend: readonly Schemas['TrendPoint'][] }) {
  return (
    <ResponsiveContainer width="100%" height={300}>
      <LineChart data={[...trend]} margin={{ top: 8, right: 12, bottom: 0, left: -12 }}>
        <CartesianGrid vertical={false} stroke="var(--grid)" />
        <XAxis dataKey="asOf" {...AXIS} tickFormatter={formatMonth} minTickGap={24} />
        <YAxis {...AXIS} axisLine={false} domain={['auto', 'auto']} width={44} />
        <Tooltip
          cursor={{ stroke: 'var(--border)' }}
          content={(props) => {
            const p = hovered<Schemas['TrendPoint']>(props)
            return p ? (
              <ChartTip>
                <div className="muted">{formatMonth(p.asOf)}</div>
                <strong>{formatScore(p.score)}</strong> score · {p.calls} calls
              </ChartTip>
            ) : null
          }}
        />
        <Line
          dataKey="score"
          stroke="var(--accent)"
          strokeWidth={2}
          dot={false}
          activeDot={{ r: 4, fill: 'var(--accent)', stroke: 'var(--surface)', strokeWidth: 2 }}
          isAnimationActive={false}
        />
      </LineChart>
    </ResponsiveContainer>
  )
}

// ---- Scored calls scatter ----

interface CallPoint {
  x: number
  y: number
  outcome: Schemas['CallOutcomeDto']
}

const dayMs = (iso: string) => Date.parse(`${iso}T00:00:00Z`)

/** A dot per call: filled when the call was right, hollow when wrong, so the result never rests on color alone. */
function CallDot(props: { cx?: number; cy?: number; payload?: CallPoint }) {
  const { cx, cy, payload } = props
  if (cx == null || cy == null || !payload) return <g />
  const color = `var(--${payload.outcome.category.toLowerCase()})`
  return payload.outcome.hit ? (
    <circle cx={cx} cy={cy} r={4.5} fill={color} stroke="var(--surface)" strokeWidth={1.5} />
  ) : (
    <circle cx={cx} cy={cy} r={4} fill="var(--surface)" stroke={color} strokeWidth={2} />
  )
}

export function OutcomeScatter({ outcomes, horizon }: { outcomes: readonly Schemas['CallOutcomeDto'][]; horizon: number }) {
  const series = CATEGORIES.map((category) => ({
    category,
    points: outcomes
      .filter((o) => o.category === category)
      .map((o): CallPoint => ({ x: dayMs(o.callDate), y: o.excessReturn * 100, outcome: o })),
  })).filter((s) => s.points.length > 0)
  const xs = series.flatMap((s) => s.points.map((p) => p.x))
  const minX = Math.min(...xs)
  const maxX = Math.max(...xs)

  return (
    <>
      <ResponsiveContainer width="100%" height={300}>
        <ScatterChart margin={{ top: 8, right: 12, bottom: 0, left: -4 }}>
          <CartesianGrid vertical={false} stroke="var(--grid)" />
          <XAxis
            type="number"
            dataKey="x"
            domain={[minX, maxX]}
            ticks={monthTicks(minX, maxX)}
            {...AXIS}
            tickFormatter={(ms: number) => formatMonth(new Date(ms).toISOString().slice(0, 10))}
          />
          <YAxis
            type="number"
            dataKey="y"
            {...AXIS}
            axisLine={false}
            width={52}
            domain={['auto', 'auto']}
            tickCount={6}
            tickFormatter={(v: number) => `${v > 0 ? '+' : ''}${v.toFixed(0)}%`}
          />
          <ReferenceLine y={0} stroke="var(--text-3)" />
          <Tooltip
            cursor={false}
            content={(props) => {
              const p = hovered<CallPoint>(props)
              if (!p) return null
              const o = p.outcome
              return (
                <ChartTip>
                  <div>
                    <strong>{o.ticker}</strong> <span className="muted">{formatDate(o.callDate)}</span>
                  </div>
                  <div>
                    {o.category} call · {formatSignedPercent(o.excessReturn)} vs benchmark
                  </div>
                  <div className="muted">{o.hit ? 'Right' : 'Wrong'}</div>
                </ChartTip>
              )
            }}
          />
          {series.map((s) => (
            <Scatter
              key={s.category}
              name={s.category}
              data={s.points}
              shape={(props: unknown) => <CallDot {...(props as Parameters<typeof CallDot>[0])} />}
              isAnimationActive={false}
            />
          ))}
        </ScatterChart>
      </ResponsiveContainer>
      <Legend
        items={[
          ...series.map((s) => ({ label: s.category, swatch: <CategorySwatch category={s.category} /> })),
          { label: 'Right', swatch: <span className="swatch-dot swatch-filled" /> },
          { label: 'Wrong', swatch: <span className="swatch-dot swatch-hollow" /> },
        ]}
      />
      <p className="chart-note">
        Each dot is one call, placed by when it was made and how the stock did against the benchmark over the next{' '}
        {horizon} months.
      </p>
    </>
  )
}

export function CategorySwatch({ category }: { category: Category }) {
  return <span className={`swatch-dot dot-${category}`} />
}
