import { Link, useSearchParams } from 'react-router'
import { useFeed, useSectors, useStockOptions } from '../api/queries'
import { Pager, Segmented } from '../components/Controls'
import { ActionBadge, CategoryDot, RatingChange, TargetChange } from '../components/Ratings'
import { EmptyState, ErrorState } from '../components/States'
import { feedFilters, feedSearchParams, parseFeedParams, type FeedState, type FeedView } from '../lib/feedParams'
import { formatCount, formatDate, formatPrice, formatTime } from '../lib/format'

const VIEWS: { value: FeedView; label: string }[] = [
  { value: 'changes', label: 'All changes' },
  { value: 'Upgrade', label: 'Upgrades' },
  { value: 'Downgrade', label: 'Downgrades' },
  { value: 'Initiate', label: 'Initiations' },
  { value: 'all', label: 'Every call' },
]

export function FeedPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const state = parseFeedParams(searchParams)
  const update = (changes: Partial<FeedState>) =>
    setSearchParams(feedSearchParams({ ...state, page: 1, ...changes }))

  const feed = useFeed(feedFilters(state))
  const stocks = useStockOptions()
  const sectors = useSectors()

  return (
    <>
      <title>Live feed · Analyst Scorecard</title>
      <div className="page-head">
        <h1>Live feed</h1>
        <p className="lede">
          Upgrades, downgrades and initiations across the tracked stocks, newest first. New calls arrive with the daily
          ingestion; this page checks for them every minute.
        </p>
      </div>

      <div className="filters" role="group" aria-label="Feed filters">
        <Segmented label="Show" options={VIEWS} value={state.view} onChange={(view) => update({ view })} />
        <label className="field">
          <span>Stock</span>
          <select value={state.ticker ?? ''} onChange={(e) => update({ ticker: e.target.value || null })}>
            <option value="">All stocks</option>
            {state.ticker && !stocks.data?.items.some((s) => s.ticker === state.ticker) && (
              <option value={state.ticker}>{state.ticker}</option>
            )}
            {stocks.data?.items
              .filter((s) => !s.isBenchmark)
              .map((s) => (
                <option key={s.ticker} value={s.ticker}>
                  {s.ticker} · {s.name}
                </option>
              ))}
          </select>
        </label>
        <label className="field">
          <span>Sector</span>
          <select value={state.sector ?? ''} onChange={(e) => update({ sector: e.target.value || null })}>
            <option value="">All sectors</option>
            {sectors.data?.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </label>
      </div>

      {feed.isPending ? (
        <div className="chart-skeleton chart-skeleton-tall" />
      ) : feed.isError && !feed.data ? (
        <ErrorState error={feed.error} onRetry={() => feed.refetch()} />
      ) : feed.data.items.length === 0 ? (
        <EmptyState
          title="No calls match"
          action={
            (state.ticker || state.sector || state.view !== 'changes') && (
              <button type="button" className="button" onClick={() => setSearchParams({})}>
                Clear filters
              </button>
            )
          }
        />
      ) : (
        <>
          <p className="summary" aria-live="polite">
            {formatCount(feed.data.totalCount)} calls · checked at {formatTime(new Date(feed.dataUpdatedAt))}
            {feed.isError && ' · last check failed, retrying'}
          </p>
          <div className="table-wrap" aria-busy={feed.isPlaceholderData}>
            <table className={feed.isPlaceholderData ? 'table is-stale' : 'table'}>
              <thead>
                <tr>
                  <th scope="col">Date</th>
                  <th scope="col">Stock</th>
                  <th scope="col">Firm</th>
                  <th scope="col">Action</th>
                  <th scope="col">Rating</th>
                  <th scope="col" className="num col-optional">Target</th>
                  <th scope="col" className="num col-optional">Price at call</th>
                </tr>
              </thead>
              <tbody>
                {feed.data.items.map((r) => (
                  <tr key={r.id}>
                    <td>{formatDate(r.date)}</td>
                    <td className="col-name">
                      <Link className="ticker" to={`/stocks/${r.ticker}`}>
                        {r.ticker}
                      </Link>
                      <span className="sub">{r.stockName}</span>
                    </td>
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
          <Pager
            page={state.page}
            totalPages={feed.data.totalPages}
            onPage={(page) => setSearchParams(feedSearchParams({ ...state, page }))}
          />
        </>
      )}
    </>
  )
}
