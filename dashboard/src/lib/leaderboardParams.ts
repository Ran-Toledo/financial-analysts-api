// Leaderboard filters live in the URL query string, so any view can be bookmarked or shared.

export type Subject = 'firms' | 'analysts'
export type SortDir = 'asc' | 'desc'

export const SORT_KEYS = ['score', 'hitRate', 'avgExcessReturn', 'calls', 'name'] as const
export type SortKey = (typeof SORT_KEYS)[number]

/** Must match the API's Scoring:HorizonsMonths; an unknown horizon comes back as a 400. */
export const HORIZONS = [3, 6, 12] as const
export const DEFAULT_HORIZON = 12

export interface LeaderboardState {
  subject: Subject
  horizon: number
  sector: string | null
  /** Analysts only. */
  firmId: number | null
  sort: SortKey
  dir: SortDir
  page: number
}

/** Best first for every key except name, matching the API. */
export function defaultDir(sort: SortKey): SortDir {
  return sort === 'name' ? 'asc' : 'desc'
}

function positiveInt(value: string | null): number | null {
  if (value == null || !/^\d+$/.test(value)) return null
  const n = Number(value)
  return n >= 1 ? n : null
}

/** Reads the URL; anything missing or invalid falls back to the default. */
export function parseLeaderboardParams(params: URLSearchParams): LeaderboardState {
  // FMP's free tier names firms only, so firms are the default view.
  const subject: Subject = params.get('view') === 'analysts' ? 'analysts' : 'firms'

  const horizonParam = positiveInt(params.get('horizon'))
  const horizon = HORIZONS.find((h) => h === horizonParam) ?? DEFAULT_HORIZON

  const sortParam = params.get('sort')
  const sort = SORT_KEYS.find((k) => k === sortParam) ?? 'score'

  const dirParam = params.get('dir')
  const dir: SortDir = dirParam === 'asc' || dirParam === 'desc' ? dirParam : defaultDir(sort)

  return {
    subject,
    horizon,
    sector: params.get('sector') || null,
    firmId: subject === 'analysts' ? positiveInt(params.get('firm')) : null,
    sort,
    dir,
    page: positiveInt(params.get('page')) ?? 1,
  }
}

/** Writes only what differs from the defaults, to keep URLs short. */
export function toSearchParams(state: LeaderboardState): URLSearchParams {
  const params = new URLSearchParams()
  if (state.subject !== 'firms') params.set('view', state.subject)
  if (state.horizon !== DEFAULT_HORIZON) params.set('horizon', String(state.horizon))
  if (state.sector) params.set('sector', state.sector)
  if (state.subject === 'analysts' && state.firmId != null) params.set('firm', String(state.firmId))
  if (state.sort !== 'score') params.set('sort', state.sort)
  if (state.dir !== defaultDir(state.sort)) params.set('dir', state.dir)
  if (state.page > 1) params.set('page', String(state.page))
  return params
}

/** Clicking the active column flips it; a new column starts best-first. Either way, back to page 1. */
export function toggleSort(state: LeaderboardState, key: SortKey): LeaderboardState {
  const dir = state.sort === key ? (state.dir === 'asc' ? 'desc' : 'asc') : defaultDir(key)
  return { ...state, sort: key, dir, page: 1 }
}

/** Any filter change goes back to page 1; switching to firms drops the firm filter. */
export function withFilters(state: LeaderboardState, changes: Partial<LeaderboardState>): LeaderboardState {
  const next = { ...state, ...changes, page: 1 }
  if (next.subject === 'firms') next.firmId = null
  return next
}
