import { keepPreviousData, useQuery } from '@tanstack/react-query'
import type { LeaderboardState } from '../lib/leaderboardParams'
import { api, unwrap } from './client'

export const LEADERBOARD_PAGE_SIZE = 25
const TREND_MONTHS = 12

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

/** Firms for the analyst leaderboard's firm filter (the first 100 by name). */
export function useFirmOptions(enabled: boolean) {
  return useQuery({
    queryKey: ['firm-options'],
    queryFn: ({ signal }) =>
      unwrap(api.GET('/api/firms', { params: { query: { Sort: 'name', PageSize: 100 } }, signal })),
    enabled,
    staleTime: Infinity,
  })
}
