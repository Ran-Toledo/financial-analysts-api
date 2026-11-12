import { keepPreviousData, useQuery } from '@tanstack/react-query'
import type { LeaderboardState, Subject } from '../lib/leaderboardParams'
import { fetchAllPages } from '../lib/paging'
import { api, unwrap, type Schemas } from './client'

export const LEADERBOARD_PAGE_SIZE = 25
export const TABLE_PAGE_SIZE = 25
const TREND_MONTHS = 12

/** Rating changes, as opposed to maintained/reiterated calls and target-only moves. */
export const RATING_CHANGES: Schemas['RatingAction'][] = ['Upgrade', 'Downgrade', 'Initiate']

// ---- Leaderboard ----

export function useLeaderboard(state: LeaderboardState) {
  return useQuery({
    queryKey: ['leaderboard', state],
    queryFn: ({ signal }) => {
      const query = {
        Horizon: state.horizon,
        Sector: state.sector ?? undefined,
        FirmId: state.firmId ?? undefined,
        Sort: state.sort,
        Dir: state.dir,
        Page: state.page,
        PageSize: LEADERBOARD_PAGE_SIZE,
        TrendMonths: TREND_MONTHS,
      }
      return unwrap(
        state.subject === 'firms'
          ? api.GET('/api/leaderboards/firms', { params: { query }, signal })
          : api.GET('/api/leaderboards/analysts', { params: { query }, signal }),
      )
    },
    // Keep the current rows on screen while the next page or sort loads.
    placeholderData: keepPreviousData,
  })
}

export function useSectors() {
  return useQuery({
    queryKey: ['sectors'],
    queryFn: ({ signal }) => unwrap(api.GET('/api/stocks/sectors', { signal })),
    staleTime: Infinity,
  })
}

/** Firms for filter menus (the first 100 by name). */
export function useFirmOptions(enabled = true) {
  return useQuery({
    queryKey: ['firm-options'],
    queryFn: ({ signal }) =>
      unwrap(api.GET('/api/firms', { params: { query: { Sort: 'name', PageSize: 100 } }, signal })),
    enabled,
    staleTime: Infinity,
  })
}

/** Tracked stocks for filter menus. */
export function useStockOptions() {
  return useQuery({
    queryKey: ['stock-options'],
    queryFn: ({ signal }) =>
      unwrap(api.GET('/api/stocks', { params: { query: { Sort: 'ticker', PageSize: 100 } }, signal })),
    staleTime: Infinity,
  })
}

// ---- Firm and analyst profiles ----

export function useSubject(subject: Subject, id: number) {
  return useQuery({
    queryKey: ['subject', subject, id],
    queryFn: async ({ signal }): Promise<{ name: string; firmId: number | null; firmName: string | null; ratingCount: number }> => {
      const path = { id }
      if (subject === 'firms') {
        const firm = await unwrap(api.GET('/api/firms/{id}', { params: { path }, signal }))
        return { name: firm.name, firmId: null, firmName: null, ratingCount: firm.ratingCount }
      }
      const analyst = await unwrap(api.GET('/api/analysts/{id}', { params: { path }, signal }))
      return { name: analyst.name, firmId: analyst.firmId, firmName: analyst.firmName, ratingCount: analyst.ratingCount }
    },
  })
}

export function useScoreCard(subject: Subject, id: number, trendHorizon: number) {
  return useQuery({
    queryKey: ['scorecard', subject, id, trendHorizon],
    queryFn: ({ signal }) => {
      const params = { path: { id }, query: { TrendHorizon: trendHorizon, TrendMonths: 24 } }
      return unwrap(
        subject === 'firms'
          ? api.GET('/api/firms/{id}/scores', { params, signal })
          : api.GET('/api/analysts/{id}/scores', { params, signal }),
      )
    },
    placeholderData: keepPreviousData,
  })
}

/** Every scored call at one horizon (up to 500), for the scatter plot and the calls table. */
export function useOutcomes(subject: Subject, id: number, horizon: number) {
  return useQuery({
    queryKey: ['outcomes', subject, id, horizon],
    queryFn: ({ signal }) =>
      fetchAllPages((page) => {
        const params = { path: { id }, query: { Horizon: horizon, Page: page, PageSize: 100 } }
        return unwrap(
          subject === 'firms'
            ? api.GET('/api/firms/{id}/outcomes', { params, signal })
            : api.GET('/api/analysts/{id}/outcomes', { params, signal }),
        )
      }),
  })
}

/** Calls made since `from` (newest first, up to 100): too recent to have an outcome yet. */
export function useRecentCalls(subject: Subject, id: number, from: string) {
  return useQuery({
    queryKey: ['recent-calls', subject, id, from],
    queryFn: ({ signal }) => {
      const params = { path: { id }, query: { From: from, PageSize: 100 } }
      return unwrap(
        subject === 'firms'
          ? api.GET('/api/firms/{id}/ratings', { params, signal })
          : api.GET('/api/analysts/{id}/ratings', { params, signal }),
      )
    },
  })
}

// ---- Stocks ----

export function useStocks(query: { search: string | null; sector: string | null; sort: string; dir: string; page: number }) {
  return useQuery({
    queryKey: ['stocks', query],
    queryFn: ({ signal }) =>
      unwrap(
        api.GET('/api/stocks', {
          params: {
            query: {
              Search: query.search ?? undefined,
              Sector: query.sector ?? undefined,
              Sort: query.sort,
              Dir: query.dir,
              Page: query.page,
              PageSize: 50,
            },
          },
          signal,
        }),
      ),
    placeholderData: keepPreviousData,
  })
}

export function useStock(ticker: string) {
  return useQuery({
    queryKey: ['stock', ticker],
    queryFn: ({ signal }) => unwrap(api.GET('/api/stocks/{ticker}', { params: { path: { ticker } }, signal })),
  })
}

export function usePrices(ticker: string, from: string) {
  return useQuery({
    queryKey: ['prices', ticker, from],
    queryFn: ({ signal }) =>
      unwrap(api.GET('/api/stocks/{ticker}/prices', { params: { path: { ticker }, query: { From: from } }, signal })),
    placeholderData: keepPreviousData,
  })
}

/** Upgrades, downgrades and initiations since `from` (up to 500), for the chart markers. */
export function useRatingChanges(ticker: string, from: string) {
  return useQuery({
    queryKey: ['rating-changes', ticker, from],
    queryFn: ({ signal }) =>
      fetchAllPages((page) =>
        unwrap(
          api.GET('/api/stocks/{ticker}/ratings', {
            params: { path: { ticker }, query: { From: from, Action: RATING_CHANGES, Page: page, PageSize: 100 } },
            signal,
          }),
        ),
      ),
    placeholderData: keepPreviousData,
  })
}

export function useStockRatings(ticker: string, page: number) {
  return useQuery({
    queryKey: ['stock-ratings', ticker, page],
    queryFn: ({ signal }) =>
      unwrap(
        api.GET('/api/stocks/{ticker}/ratings', {
          params: { path: { ticker }, query: { Page: page, PageSize: TABLE_PAGE_SIZE } },
          signal,
        }),
      ),
    placeholderData: keepPreviousData,
  })
}

export function useConsensus(ticker: string) {
  return useQuery({
    queryKey: ['consensus', ticker],
    queryFn: ({ signal }) => unwrap(api.GET('/api/stocks/{ticker}/consensus', { params: { path: { ticker } }, signal })),
  })
}

/** Month-end consensus; also used for the mean-target line on the price chart. */
export function useConsensusHistory(ticker: string, months: number) {
  return useQuery({
    queryKey: ['consensus-history', ticker, months],
    queryFn: ({ signal }) =>
      unwrap(
        api.GET('/api/stocks/{ticker}/consensus/history', {
          params: { path: { ticker }, query: { Months: months } },
          signal,
        }),
      ),
    placeholderData: keepPreviousData,
  })
}

// ---- Feed ----

export interface FeedFilters {
  ticker: string | null
  sector: string | null
  /** Empty means the API default: rating changes only. */
  actions: Schemas['RatingAction'][]
  includeAll: boolean
  page: number
}

/** Polls every minute; new ratings arrive once a day, but the page may stay open across an ingestion run. */
export function useFeed(filters: FeedFilters) {
  return useQuery({
    queryKey: ['feed', filters],
    queryFn: ({ signal }) =>
      unwrap(
        api.GET('/api/feed', {
          params: {
            query: {
              Ticker: filters.ticker ?? undefined,
              Sector: filters.sector ?? undefined,
              Action: filters.actions.length > 0 ? filters.actions : undefined,
              IncludeAll: filters.includeAll || undefined,
              Page: filters.page,
              PageSize: TABLE_PAGE_SIZE,
            },
          },
          signal,
        }),
      ),
    placeholderData: keepPreviousData,
    refetchInterval: 60_000,
    staleTime: 0,
  })
}
