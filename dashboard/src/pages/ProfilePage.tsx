import { useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import type { Schemas } from '../api/client'
import { TABLE_PAGE_SIZE, useOutcomes, useRecentCalls, useScoreCard, useSubject } from '../api/queries'
import { Card, Pager, Segmented } from '../components/Controls'
import { OutcomeScatter, ScoreTrendChart } from '../components/ProfileCharts'
import { ActionBadge, CategoryDot, RatingChange } from '../components/Ratings'
import { EmptyState, ErrorState } from '../components/States'
import { addMonths, todayIso } from '../lib/dates'
import {
  formatCount,
  formatDate,
  formatPercent,
  formatScore,
  formatSignedPercent,
} from '../lib/format'
import { DEFAULT_HORIZON, HORIZONS, type Subject } from '../lib/leaderboardParams'
import { NotFoundPage } from './NotFoundPage'
import { ApiError } from '../lib/errors'

/** Scorecard for a research firm (/firms/:id) or an analyst (/analysts/:id). */
export function ProfilePage({ subject }: { subject: Subject }) {
  const id = Number(useParams().id)
  const [searchParams, setSearchParams] = useSearchParams()
  const horizonParam = Number(searchParams.get('horizon'))
  const horizon = HORIZONS.find((h) => h === horizonParam) ?? DEFAULT_HORIZON
  const setHorizon = (h: number) =>
    setSearchParams(h === DEFAULT_HORIZON ? {} : { horizon: String(h) }, { replace: true })

  const info = useSubject(subject, id)
  const card = useScoreCard(subject, id, horizon)

  if (!Number.isInteger(id) || id < 1) return <NotFoundPage />
  if (info.error instanceof ApiError && info.error.status === 404) return <NotFoundPage />
  if (info.isError) return <ErrorState error={info.error} onRetry={() => info.refetch()} />

  const noun = subject === 'firms' ? 'Firms' : 'Analysts'
  const selected = card.data?.horizons.find((h) => h.horizonMonths === horizon)

  return (
    <>
      <title>{`${info.data?.name ?? noun} · Analyst Scorecard`}</title>
      <nav className="crumbs" aria-label="Breadcrumb">
        <Link to={subject === 'firms' ? '/' : '/?view=analysts'}>Leaderboard</Link> / {noun}
      </nav>
      <div className="page-head">
        <h1>{info.data?.name ?? ' '}</h1>
        <p className="lede">
          {info.data?.firmId != null && (
            <>
              Analyst at <Link to={`/firms/${info.data.firmId}`}>{info.data.firmName}</Link> ·{' '}
            </>
          )}
          {info.data && `${formatCount(info.data.ratingCount)} calls on record`}
        </p>
      </div>

      {card.isError && !card.data ? (
        <ErrorState error={card.error} onRetry={() => card.refetch()} />
      ) : (
        <div className="tiles" role="group" aria-label="Horizon">
          {(card.data?.horizons ?? HORIZONS.map((h) => ({ horizonMonths: h, stats: null, rank: null, rankedCount: 0 }))).map(
            (h) => (
              <HorizonTile
                key={h.horizonMonths}
                score={h}
                minCalls={card.data?.minCallsToRank}
                selected={h.horizonMonths === horizon}
                loading={card.isPending}
                onSelect={() => setHorizon(h.horizonMonths)}
              />
            ),
          )}
        </div>
      )}

      <div className="grid-2">
        <Card title={`Score over time · ${horizon}-month horizon`}>
          {card.isPending ? (
            <div className="chart-skeleton" />
          ) : card.data && card.data.trend.length >= 2 ? (
            <ScoreTrendChart trend={card.data.trend} />
          ) : (
            <EmptyState title="Not enough history">Scores appear once calls have matured for a couple of months.</EmptyState>
          )}
        </Card>
        <Card title="How each call turned out">
          <OutcomesScatterCard subject={subject} id={id} horizon={horizon} />
        </Card>
      </div>

      <ScoredCalls key={`scored-${horizon}`} subject={subject} id={id} horizon={horizon} hasScore={selected?.stats != null} />
      <PendingCalls key={`pending-${horizon}`} subject={subject} id={id} horizon={horizon} />
    </>
  )
}

function HorizonTile({
  score,
  minCalls,
  selected,
  loading,
  onSelect,
}: {
  score: Schemas['HorizonScoreDto']
  minCalls: number | undefined
  selected: boolean
  loading: boolean
  onSelect: () => void
}) {
  const s = score.stats
  return (
    <button type="button" className="tile" aria-pressed={selected} onClick={onSelect}>
      <span className="tile-label">{score.horizonMonths}-month horizon</span>
      <span className="tile-value">{loading ? ' ' : s ? formatScore(s.score) : '—'}</span>
      <span className="tile-sub">
        {loading
          ? ' '
          : score.rank != null
            ? `#${score.rank} of ${score.rankedCount}`
            : s
              ? `Not ranked (needs ${minCalls} calls)`
              : 'No scored calls yet'}
      </span>
      {s && (
        <dl className="tile-stats">
          <div>
            <dt>Hit rate</dt>
            <dd>{formatPercent(s.hitRate)}</dd>
          </div>
          <div>
            <dt>Excess return</dt>
            <dd>{formatSignedPercent(s.avgExcessReturn)}</dd>
          </div>
          <div>
            <dt>Calls</dt>
            <dd>{formatCount(s.calls)}</dd>
          </div>
        </dl>
      )}
    </button>
  )
}

function OutcomesScatterCard({ subject, id, horizon }: { subject: Subject; id: number; horizon: number }) {
  const outcomes = useOutcomes(subject, id, horizon)
  if (outcomes.isPending) return <div className="chart-skeleton" />
  if (outcomes.isError) return <ErrorState error={outcomes.error} onRetry={() => outcomes.refetch()} />
  if (outcomes.data.items.length === 0)
    return <EmptyState title="No scored calls yet">Calls are scored once {horizon} months have passed.</EmptyState>
  return <OutcomeScatter outcomes={outcomes.data.items} horizon={horizon} />
}

type ResultFilter = 'all' | 'right' | 'wrong'

function ScoredCalls({ subject, id, horizon, hasScore }: { subject: Subject; id: number; horizon: number; hasScore: boolean }) {
  const outcomes = useOutcomes(subject, id, horizon)
  const [filter, setFilter] = useState<ResultFilter>('all')
  const [page, setPage] = useState(1)

  const all = outcomes.data?.items ?? []
  const rows = filter === 'all' ? all : all.filter((o) => o.hit === (filter === 'right'))
  const totalPages = Math.ceil(rows.length / TABLE_PAGE_SIZE)
  const shown = rows.slice((page - 1) * TABLE_PAGE_SIZE, page * TABLE_PAGE_SIZE)
  const showAnalyst = subject === 'firms' && all.some((o) => o.analystName)

  return (
    <Card
      title="Scored calls"
      actions={
        hasScore && (
          <Segmented<ResultFilter>
            label="Result"
            value={filter}
            onChange={(f) => {
              setFilter(f)
              setPage(1)
            }}
            options={[
              { value: 'all', label: 'All' },
              { value: 'right', label: 'Right' },
              { value: 'wrong', label: 'Wrong' },
            ]}
          />
        )
      }
    >
      {outcomes.isPending ? (
        <div className="chart-skeleton" />
      ) : outcomes.isError ? (
        <ErrorState error={outcomes.error} onRetry={() => outcomes.refetch()} />
      ) : rows.length === 0 ? (
        <EmptyState title={all.length === 0 ? 'No scored calls yet' : 'No calls match'} />
      ) : (
        <>
          {outcomes.data.truncated && <p className="chart-note">Showing the 500 most recent scored calls.</p>}
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th scope="col">Called</th>
                  <th scope="col">Stock</th>
                  {showAnalyst && <th scope="col" className="col-optional">Analyst</th>}
                  <th scope="col">Call</th>
                  <th scope="col" className="num col-optional">Return</th>
                  <th scope="col" className="num" title="Return minus the benchmark's over the same period">
                    vs benchmark
                  </th>
                  <th scope="col">Result</th>
                </tr>
              </thead>
              <tbody>
                {shown.map((o) => (
                  <tr key={o.ratingId}>
                    <td>{formatDate(o.callDate)}</td>
                    <td>
                      <Link className="ticker" to={`/stocks/${o.ticker}`}>
                        {o.ticker}
                      </Link>
                    </td>
                    {showAnalyst && <td className="col-optional">{o.analystName ?? <span className="muted">—</span>}</td>}
                    <td>
                      <CategoryDot category={o.category} /> {o.category}
                    </td>
                    <td className="num col-optional">{formatSignedPercent(o.return)}</td>
                    <td className="num">{formatSignedPercent(o.excessReturn)}</td>
                    <td>
                      <span className={o.hit ? 'result result-right' : 'result result-wrong'}>
                        <span aria-hidden="true">{o.hit ? '✓' : '✗'}</span> {o.hit ? 'Right' : 'Wrong'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pager page={page} totalPages={totalPages} onPage={setPage} />
        </>
      )}
    </Card>
  )
}

function PendingCalls({ subject, id, horizon }: { subject: Subject; id: number; horizon: number }) {
  const from = addMonths(todayIso(), -horizon)
  const calls = useRecentCalls(subject, id, from)
  const [page, setPage] = useState(1)
  const all = calls.data?.items ?? []
  const shown = all.slice((page - 1) * TABLE_PAGE_SIZE, page * TABLE_PAGE_SIZE)

  return (
    <Card title={`Not scored yet · last ${horizon} months`}>
      {calls.isPending ? (
        <div className="chart-skeleton" />
      ) : calls.isError ? (
        <ErrorState error={calls.error} onRetry={() => calls.refetch()} />
      ) : calls.data.items.length === 0 ? (
        <EmptyState title="No recent calls" />
      ) : (
        <>
          <p className="chart-note">
            These calls are younger than the horizon, so there's no outcome yet. Each is scored {horizon} months after
            it was made.
            {calls.data.totalCount > calls.data.items.length && ` Showing the newest ${calls.data.items.length}.`}
          </p>
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th scope="col">Called</th>
                  <th scope="col">Stock</th>
                  <th scope="col">Action</th>
                  <th scope="col">Rating</th>
                  <th scope="col" className="col-optional">Scored on</th>
                </tr>
              </thead>
              <tbody>
                {shown.map((r) => (
                  <tr key={r.id}>
                    <td>{formatDate(r.date)}</td>
                    <td>
                      <Link className="ticker" to={`/stocks/${r.ticker}`}>
                        {r.ticker}
                      </Link>
                    </td>
                    <td>
                      <ActionBadge action={r.action} />
                    </td>
                    <td>
                      <CategoryDot category={r.category} /> <RatingChange rating={r} />
                    </td>
                    <td className="col-optional muted">{formatDate(addMonths(r.date, horizon))}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pager page={page} totalPages={Math.ceil(all.length / TABLE_PAGE_SIZE)} onPage={setPage} />
        </>
      )}
    </Card>
  )
}
