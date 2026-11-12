import { useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import {
  useConsensus,
  useConsensusHistory,
  usePrices,
  useRatingChanges,
  useStock,
  useStockRatings,
} from '../api/queries'
import { ConsensusHistoryChart, ConsensusPanel } from '../components/Consensus'
import { Card, Pager, Segmented } from '../components/Controls'
import { PriceChart } from '../components/PriceChart'
import { ActionBadge, CategoryDot, RatingChange, TargetChange } from '../components/Ratings'
import { EmptyState, ErrorState } from '../components/States'
import { addMonths, todayIso } from '../lib/dates'
import { ApiError } from '../lib/errors'
import { formatDate, formatPrice } from '../lib/format'
import { NotFoundPage } from './NotFoundPage'

const RANGES = [
  { value: 6, label: '6M' },
  { value: 12, label: '1Y' },
  { value: 36, label: '3Y' },
] as const

export function StockPage() {
  const ticker = (useParams().ticker ?? '').toUpperCase()
  const [searchParams, setSearchParams] = useSearchParams()
  const rangeParam = Number(searchParams.get('range'))
  const months = RANGES.find((r) => r.value === rangeParam)?.value ?? 12
  const from = addMonths(todayIso(), -months)

  const stock = useStock(ticker)
  const prices = usePrices(ticker, from)
  const changes = useRatingChanges(ticker, from)
  const history = useConsensusHistory(ticker, months)
  const yearHistory = useConsensusHistory(ticker, 12)
  const consensus = useConsensus(ticker)

  if (stock.error instanceof ApiError && stock.error.status === 404) return <NotFoundPage />
  if (stock.isError) return <ErrorState error={stock.error} onRetry={() => stock.refetch()} />

  const s = stock.data
  return (
    <>
      <title>{`${ticker}${s ? ` · ${s.name}` : ''} · Analyst Scorecard`}</title>
      <nav className="crumbs" aria-label="Breadcrumb">
        <Link to="/stocks">Stocks</Link> / {ticker}
      </nav>
      <div className="page-head stock-head">
        <div>
          <h1>
            {ticker} <span className="h1-sub">{s?.name}</span>
          </h1>
          <p className="lede">{s && [s.sector, s.industry, s.exchange].filter(Boolean).join(' · ')}</p>
        </div>
        {s?.lastClose != null && (
          <div className="last-close">
            <span className="last-close-value">{formatPrice(s.lastClose)}</span>
            <span className="muted">Close on {formatDate(s.lastCloseDate)}</span>
          </div>
        )}
      </div>

      <Card
        title="Price and rating changes"
        actions={
          <Segmented
            label="Range"
            options={RANGES}
            value={months}
            onChange={(m) => setSearchParams(m === 12 ? {} : { range: String(m) }, { replace: true })}
          />
        }
      >
        {prices.isPending || changes.isPending ? (
          <div className="chart-skeleton chart-skeleton-tall" />
        ) : prices.isError ? (
          <ErrorState error={prices.error} onRetry={() => prices.refetch()} />
        ) : prices.data.length < 2 ? (
          <EmptyState title="No prices for this period" />
        ) : (
          <PriceChart prices={prices.data} changes={changes.data?.items ?? []} targets={history.data?.points ?? []} />
        )}
      </Card>

      <div className="grid-2">
        <Card title="Consensus today">
          {consensus.isPending ? (
            <div className="chart-skeleton" />
          ) : consensus.isError ? (
            <ErrorState error={consensus.error} onRetry={() => consensus.refetch()} />
          ) : (
            <ConsensusPanel consensus={consensus.data} />
          )}
        </Card>
        <Card title="Consensus by month">
          {yearHistory.isPending ? (
            <div className="chart-skeleton" />
          ) : yearHistory.isError ? (
            <ErrorState error={yearHistory.error} onRetry={() => yearHistory.refetch()} />
          ) : yearHistory.data.points.every((p) => p.contributors === 0) ? (
            <EmptyState title="No calls in the last year" />
          ) : (
            <ConsensusHistoryChart points={yearHistory.data.points} />
          )}
        </Card>
      </div>

      <RatingsTable ticker={ticker} />
    </>
  )
}

function RatingsTable({ ticker }: { ticker: string }) {
  const [page, setPage] = useState(1)
  const ratings = useStockRatings(ticker, page)

  return (
    <Card title="All calls">
      {ratings.isPending ? (
        <div className="chart-skeleton" />
      ) : ratings.isError ? (
        <ErrorState error={ratings.error} onRetry={() => ratings.refetch()} />
      ) : ratings.data.items.length === 0 ? (
        <EmptyState title="No calls on record" />
      ) : (
        <>
          <div className="table-wrap" aria-busy={ratings.isPlaceholderData}>
            <table className={ratings.isPlaceholderData ? 'table is-stale' : 'table'}>
              <thead>
                <tr>
                  <th scope="col">Date</th>
                  <th scope="col">Firm</th>
                  <th scope="col">Action</th>
                  <th scope="col">Rating</th>
                  <th scope="col" className="num col-optional">Target</th>
                  <th scope="col" className="num col-optional">Price at call</th>
                </tr>
              </thead>
              <tbody>
                {ratings.data.items.map((r) => (
                  <tr key={r.id}>
                    <td>{formatDate(r.date)}</td>
                    <td className="col-name">
                      <Link className="name" to={`/firms/${r.firmId}`}>
                        {r.firmName}
                      </Link>
                      {r.analystId != null && (
                        <Link className="sub" to={`/analysts/${r.analystId}`}>
                          {r.analystName}
                        </Link>
                      )}
                    </td>
                    <td>
                      <ActionBadge action={r.action} />
                    </td>
                    <td>
                      <CategoryDot category={r.category} /> <RatingChange rating={r} />
                    </td>
                    <td className="num col-optional">
                      <TargetChange rating={r} />
                    </td>
                    <td className="num col-optional">{formatPrice(r.priceAtCall)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pager page={page} totalPages={ratings.data.totalPages} onPage={setPage} />
        </>
      )}
    </Card>
  )
}
