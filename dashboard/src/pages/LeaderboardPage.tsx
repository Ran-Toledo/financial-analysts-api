import type { ReactNode } from 'react'
import { useSearchParams } from 'react-router'
import type { Schemas } from '../api/client'
import { LEADERBOARD_PAGE_SIZE, useFirmOptions, useLeaderboard, useSectors } from '../api/queries'
import { Sparkline } from '../components/Sparkline'
import { EmptyState, ErrorState } from '../components/States'
import { formatCount, formatPercent, formatScore, formatSignedPercent } from '../lib/format'
import {
  HORIZONS,
  parseLeaderboardParams,
  toggleSort,
  toSearchParams,
  withFilters,
  type LeaderboardState,
  type SortKey,
  type Subject,
} from '../lib/leaderboardParams'

const SUBJECTS: { value: Subject; label: string }[] = [
  { value: 'firms', label: 'Firms' },
  { value: 'analysts', label: 'Analysts' },
]

export function LeaderboardPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const state = parseLeaderboardParams(searchParams)
  const update = (next: LeaderboardState) => setSearchParams(toSearchParams(next))

  const board = useLeaderboard(state)
  const sectors = useSectors()
  const firms = useFirmOptions(state.subject === 'analysts')

  const noun = state.subject === 'firms' ? 'firms' : 'analysts'

  return (
    <>
      <title>{`Leaderboard · ${state.subject === 'firms' ? 'Firms' : 'Analysts'} · Analyst Scorecard`}</title>
      <div className="page-head">
        <h1>Leaderboard</h1>
        <p className="lede">
          Who calls it right. A Buy is right if the stock beat the benchmark over the horizon, a Sell if it lagged, and a
          Hold if it stayed close. The score (0–100) blends hit rate and excess return, pulled toward the average for
          small samples.
        </p>
      </div>

      <div className="filters" role="group" aria-label="Leaderboard filters">
        <div className="segmented" role="group" aria-label="Rank">
          {SUBJECTS.map((s) => (
            <button
              key={s.value}
              type="button"
              aria-pressed={state.subject === s.value}
              onClick={() => update(withFilters(state, { subject: s.value }))}
            >
              {s.label}
            </button>
          ))}
        </div>

        <label className="field">
          <span>Horizon</span>
          <select
            value={state.horizon}
            onChange={(e) => update(withFilters(state, { horizon: Number(e.target.value) }))}
          >
            {HORIZONS.map((h) => (
              <option key={h} value={h}>
                {h} months
              </option>
            ))}
          </select>
        </label>

        <label className="field">
          <span>Sector</span>
          <select
            value={state.sector ?? ''}
            onChange={(e) => update(withFilters(state, { sector: e.target.value || null }))}
          >
            <option value="">All sectors</option>
            {state.sector && !sectors.data?.includes(state.sector) && <option value={state.sector}>{state.sector}</option>}
            {sectors.data?.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </label>

        {state.subject === 'analysts' && (
          <label className="field">
            <span>Firm</span>
            <select
              value={state.firmId ?? ''}
              onChange={(e) => update(withFilters(state, { firmId: e.target.value ? Number(e.target.value) : null }))}
            >
              <option value="">All firms</option>
              {firms.data?.items.map((f) => (
                <option key={f.id} value={f.id}>
                  {f.name}
                </option>
              ))}
            </select>
          </label>
        )}
      </div>

      {board.isPending ? (
        <TableSkeleton />
      ) : board.isError && !board.data ? (
        <ErrorState error={board.error} onRetry={() => board.refetch()} />
      ) : (
        <Results
          board={board.data}
          state={state}
          noun={noun}
          stale={board.isPlaceholderData}
          error={board.isError ? board.error : null}
          onRetry={() => board.refetch()}
          update={update}
        />
      )}
    </>
  )
}

function Results({
  board,
  state,
  noun,
  stale,
  error,
  onRetry,
  update,
}: {
  board: Schemas['LeaderboardDto']
  state: LeaderboardState
  noun: string
  stale: boolean
  error: unknown
  onRetry: () => void
  update: (next: LeaderboardState) => void
}) {
  const filtered = state.sector != null || state.firmId != null

  if (board.items.length === 0) {
    if (board.totalCount > 0) {
      return (
        <EmptyState
          title="This page is past the end"
          action={<button type="button" className="button" onClick={() => update({ ...state, page: 1 })}>Go to page 1</button>}
        >
          There are {board.totalPages} pages of {noun}.
        </EmptyState>
      )
    }
    if (state.subject === 'analysts' && !filtered) {
      return (
        <EmptyState
          title="No analyst scores yet"
          action={
            <button type="button" className="button" onClick={() => update(withFilters(state, { subject: 'firms' }))}>
              Show firms
            </button>
          }
        >
          The data provider hasn't named individual analysts (FMP's free tier names research firms only), so only
          firms are scored.
        </EmptyState>
      )
    }
    return (
      <EmptyState
        title={`No ${noun} ranked`}
        action={
          filtered && (
            <button
              type="button"
              className="button"
              onClick={() => update(withFilters(state, { sector: null, firmId: null }))}
            >
              Clear filters
            </button>
          )
        }
      >
        Ranking needs at least {board.minCalls} matured calls at the {board.horizonMonths}-month horizon
        {filtered ? ' for these filters' : ''}.
      </EmptyState>
    )
  }

  const sortHeader = (key: SortKey, label: string, className?: string, hint?: string) => (
    <SortHeader state={state} sortKey={key} className={className} hint={hint} onSort={(k) => update(toggleSort(state, k))}>
      {label}
    </SortHeader>
  )

  return (
    <>
      <p className="summary">
        {formatCount(board.totalCount)} {noun} ranked · at least {board.minCalls} matured calls each ·{' '}
        {board.horizonMonths}-month horizon{board.sector ? ` · ${board.sector}` : ''}
      </p>

      {error != null && <ErrorState error={error} onRetry={onRetry} />}

      <div className="table-wrap" aria-busy={stale}>
        <table className={stale ? 'table is-stale' : 'table'}>
          <thead>
            <tr>
              <th scope="col" className="num col-rank">#</th>
              {sortHeader('name', state.subject === 'firms' ? 'Firm' : 'Analyst', 'col-name')}
              {sortHeader('score', 'Score', 'col-score')}
              {sortHeader('hitRate', 'Hit rate', 'num', 'Share of calls that were right')}
              {sortHeader('avgExcessReturn', 'Excess return', 'num col-optional', 'Average return over the benchmark')}
              {sortHeader('calls', 'Calls', 'num', 'Matured calls')}
              <th scope="col" className="col-trend col-optional">12-month trend</th>
            </tr>
          </thead>
          <tbody>
            {board.items.map((row) => (
              <tr key={row.id}>
                <td className="num col-rank">{row.rank}</td>
                <td className="col-name">
                  <span className="name">{row.name}</span>
                  {row.firmName && <span className="sub">{row.firmName}</span>}
                </td>
                <td className="col-score">
                  <ScoreMeter score={row.stats.score} />
                </td>
                <td className="num">{formatPercent(row.stats.hitRate)}</td>
                <td className="num col-optional">{formatSignedPercent(row.stats.avgExcessReturn)}</td>
                <td className="num">{formatCount(row.stats.calls)}</td>
                <td className="col-trend col-optional">
                  <Sparkline trend={row.trend ?? []} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {board.totalPages > 1 && (
        <nav className="pager" aria-label="Pages">
          <button
            type="button"
            className="button"
            disabled={state.page <= 1}
            onClick={() => update({ ...state, page: state.page - 1 })}
          >
            Previous
          </button>
          <span>
            Page {board.page} of {board.totalPages}
          </span>
          <button
            type="button"
            className="button"
            disabled={state.page >= board.totalPages}
            onClick={() => update({ ...state, page: state.page + 1 })}
          >
            Next
          </button>
        </nav>
      )}
    </>
  )
}

function SortHeader({
  state,
  sortKey,
  className,
  hint,
  onSort,
  children,
}: {
  state: LeaderboardState
  sortKey: SortKey
  className?: string
  hint?: string
  onSort: (key: SortKey) => void
  children: ReactNode
}) {
  const active = state.sort === sortKey
  return (
    <th
      scope="col"
      className={className}
      title={hint}
      aria-sort={active ? (state.dir === 'asc' ? 'ascending' : 'descending') : 'none'}
    >
      <button type="button" className="sort" onClick={() => onSort(sortKey)}>
        {children}
        <span className="sort-arrow" aria-hidden="true">
          {active ? (state.dir === 'asc' ? '▲' : '▼') : ''}
        </span>
      </button>
    </th>
  )
}

function ScoreMeter({ score }: { score: number }) {
  return (
    <span className="score">
      <span className="score-value">{formatScore(score)}</span>
      <span className="meter" aria-hidden="true">
        <span style={{ width: `${Math.max(0, Math.min(100, score))}%` }} />
      </span>
    </span>
  )
}

function TableSkeleton() {
  return (
    <div className="table-wrap" aria-busy="true" aria-label="Loading leaderboard">
      <table className="table skeleton">
        <tbody>
          {Array.from({ length: Math.min(LEADERBOARD_PAGE_SIZE, 10) }, (_, i) => (
            <tr key={i}>
              <td colSpan={7}>
                <span className="skeleton-bar" />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
