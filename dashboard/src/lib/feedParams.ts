import type { FeedFilters } from '../api/queries'

/** What the feed shows: the API's three rating-change actions one at a time, all of them, or every call. */
export const FEED_VIEWS = ['changes', 'Upgrade', 'Downgrade', 'Initiate', 'all'] as const
export type FeedView = (typeof FEED_VIEWS)[number]

export interface FeedState {
  view: FeedView
  ticker: string | null
  sector: string | null
  page: number
}

export function parseFeedParams(params: URLSearchParams): FeedState {
  const view = FEED_VIEWS.find((v) => v === params.get('show')) ?? 'changes'
  const ticker = params.get('ticker')?.toUpperCase() ?? null
  const page = Number(params.get('page'))
  return {
    view,
    ticker: ticker && /^[A-Z][A-Z.-]{0,9}$/.test(ticker) /* the API's ticker pattern */ ? ticker : null,
    sector: params.get('sector') || null,
    page: Number.isInteger(page) && page > 1 ? page : 1,
  }
}

export function feedSearchParams(state: FeedState): URLSearchParams {
  const params = new URLSearchParams()
  if (state.view !== 'changes') params.set('show', state.view)
  if (state.ticker) params.set('ticker', state.ticker)
  if (state.sector) params.set('sector', state.sector)
  if (state.page > 1) params.set('page', String(state.page))
  return params
}

/** The API's filters for a view. */
export function feedFilters(state: FeedState): FeedFilters {
  return {
    ticker: state.ticker,
    sector: state.sector,
    actions: state.view === 'changes' || state.view === 'all' ? [] : [state.view],
    includeAll: state.view === 'all',
    page: state.page,
  }
}
