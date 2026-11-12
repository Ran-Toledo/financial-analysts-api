import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { useSectors, useStocks } from '../api/queries'
import { Pager } from '../components/Controls'
import { EmptyState, ErrorState } from '../components/States'
import { formatCount, formatDate, formatPrice } from '../lib/format'

const SORTS = ['ticker', 'name', 'sector', 'ratingCount'] as const
type Sort = (typeof SORTS)[number]

export function StocksPage() {
  const [params, setParams] = useSearchParams()
  const search = params.get('q') || null
  const sector = params.get('sector') || null
  const sort: Sort = SORTS.find((s) => s === params.get('sort')) ?? 'ticker'
  const dir = params.get('dir') === 'desc' || (params.get('dir') == null && sort === 'ratingCount') ? 'desc' : 'asc'
  const page = Math.max(1, Number(params.get('page')) || 1)

  const set = (changes: Record<string, string | null>) => {
    const next = new URLSearchParams(params)
    next.delete('page')
    for (const [k, v] of Object.entries(changes)) {
      if (v) next.set(k, v)
      else next.delete(k)
    }
    setParams(next, { replace: 'q' in changes })
  }

  // Search as you type, a beat after the last keystroke.
  const [draft, setDraft] = useState(search ?? '')
  useEffect(() => setDraft(search ?? ''), [search])
  useEffect(() => {
    if (draft.trim() === (search ?? '')) return
    const t = setTimeout(() => set({ q: draft.trim() || null }), 250)
    return () => clearTimeout(t)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draft])

  const stocks = useStocks({ search, sector, sort, dir, page })
  const sectors = useSectors()

  const header = (key: Sort, label: string, className?: string) => {
    const active = sort === key
    return (
      <th scope="col" className={className} aria-sort={active ? (dir === 'asc' ? 'ascending' : 'descending') : 'none'}>
        <button
          type="button"
          className="sort"
          onClick={() =>
            set({ sort: key === 'ticker' ? null : key, dir: active ? (dir === 'asc' ? 'desc' : 'asc') : null })
          }
        >
          {label}
          <span className="sort-arrow" aria-hidden="true">
            {active ? (dir === 'asc' ? '▲' : '▼') : ''}
          </span>
        </button>
      </th>
    )
  }

  return (
    <>
      <title>Stocks · Analyst Scorecard</title>
      <div className="page-head">
        <h1>Stocks</h1>
        <p className="lede">The tracked universe. Pick a stock for its price chart, consensus and every call on record.</p>
      </div>

      <div className="filters" role="search">
        <label className="field">
          <span>Search</span>
          <input type="search" value={draft} placeholder="Ticker or name" onChange={(e) => setDraft(e.target.value)} />
        </label>
        <label className="field">
          <span>Sector</span>
          <select value={sector ?? ''} onChange={(e) => set({ sector: e.target.value || null })}>
            <option value="">All sectors</option>
            {sectors.data?.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </label>
      </div>

      {stocks.isPending ? (
        <div className="chart-skeleton chart-skeleton-tall" />
      ) : stocks.isError && !stocks.data ? (
        <ErrorState error={stocks.error} onRetry={() => stocks.refetch()} />
      ) : stocks.data.items.length === 0 ? (
        <EmptyState title="No stocks match" />
      ) : (
        <>
          <div className="table-wrap" aria-busy={stocks.isPlaceholderData}>
            <table className={stocks.isPlaceholderData ? 'table is-stale' : 'table'}>
              <thead>
                <tr>
                  {header('ticker', 'Ticker')}
                  {header('name', 'Company', 'col-optional')}
                  {header('sector', 'Sector')}
                  <th scope="col" className="num">Last close</th>
                  {header('ratingCount', 'Calls', 'num')}
                </tr>
              </thead>
              <tbody>
                {stocks.data.items.map((s) => (
                  <tr key={s.id}>
                    <td>
                      <Link className="ticker" to={`/stocks/${s.ticker}`}>
                        {s.ticker}
                      </Link>
                      {s.isBenchmark && <span className="tag">Benchmark</span>}
                    </td>
                    <td className="col-optional">{s.name}</td>
                    <td>{s.sector ?? <span className="muted">—</span>}</td>
                    <td className="num" title={s.lastCloseDate ? `On ${formatDate(s.lastCloseDate)}` : undefined}>
                      {formatPrice(s.lastClose)}
                    </td>
                    <td className="num">{formatCount(s.ratingCount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pager
            page={page}
            totalPages={stocks.data.totalPages}
            onPage={(p) => {
              const next = new URLSearchParams(params)
              next.set('page', String(p))
              setParams(next)
            }}
          />
        </>
      )}
    </>
  )
}
