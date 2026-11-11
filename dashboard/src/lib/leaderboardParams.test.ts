import { describe, expect, it } from 'vitest'
import { parseLeaderboardParams, toggleSort, toSearchParams, withFilters, type LeaderboardState } from './leaderboardParams'

const parse = (query: string) => parseLeaderboardParams(new URLSearchParams(query))

const defaults: LeaderboardState = {
  subject: 'firms',
  horizon: 12,
  sector: null,
  firmId: null,
  sort: 'score',
  dir: 'desc',
  page: 1,
}

describe('parseLeaderboardParams', () => {
  it('defaults to firms by score at 12 months', () => {
    expect(parse('')).toEqual(defaults)
  })

  it('reads every filter', () => {
    expect(parse('view=analysts&horizon=6&sector=Technology&firm=22&sort=hitRate&dir=asc&page=3')).toEqual({
      subject: 'analysts',
      horizon: 6,
      sector: 'Technology',
      firmId: 22,
      sort: 'hitRate',
      dir: 'asc',
      page: 3,
    })
  })

  it('ignores invalid values', () => {
    expect(parse('view=nope&horizon=5&sort=id;drop&dir=up&page=-2&firm=abc')).toEqual(defaults)
    expect(parse('page=0').page).toBe(1)
    expect(parse('page=2.5').page).toBe(1)
  })

  it('ignores the firm filter on the firm leaderboard', () => {
    expect(parse('firm=22').firmId).toBeNull()
  })

  it('sorts names A–Z unless told otherwise', () => {
    expect(parse('sort=name').dir).toBe('asc')
    expect(parse('sort=calls').dir).toBe('desc')
  })
})

describe('toSearchParams', () => {
  it('writes nothing for the defaults', () => {
    expect(toSearchParams(defaults).toString()).toBe('')
  })

  it('round-trips through the URL', () => {
    const state: LeaderboardState = { ...defaults, subject: 'analysts', horizon: 3, sector: 'Energy', firmId: 7, sort: 'name', dir: 'desc', page: 2 }
    const query = toSearchParams(state)
    expect(query.toString()).toBe('view=analysts&horizon=3&sector=Energy&firm=7&sort=name&dir=desc&page=2')
    expect(parseLeaderboardParams(query)).toEqual(state)
  })
})

describe('toggleSort', () => {
  it('flips the active column and starts a new one best-first, on page 1', () => {
    const onPage3 = { ...defaults, page: 3 }
    expect(toggleSort(onPage3, 'score')).toMatchObject({ sort: 'score', dir: 'asc', page: 1 })
    expect(toggleSort(onPage3, 'calls')).toMatchObject({ sort: 'calls', dir: 'desc', page: 1 })
    expect(toggleSort(onPage3, 'name')).toMatchObject({ sort: 'name', dir: 'asc', page: 1 })
  })
})

describe('withFilters', () => {
  it('resets the page and drops the firm filter when switching to firms', () => {
    const analysts = { ...defaults, subject: 'analysts' as const, firmId: 7, page: 4 }
    expect(withFilters(analysts, { sector: 'Energy' })).toMatchObject({ sector: 'Energy', firmId: 7, page: 1 })
    expect(withFilters(analysts, { subject: 'firms' })).toMatchObject({ subject: 'firms', firmId: null, page: 1 })
  })
})
