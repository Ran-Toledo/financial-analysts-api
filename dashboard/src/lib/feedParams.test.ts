import { describe, expect, it } from 'vitest'
import { feedFilters, feedSearchParams, parseFeedParams } from './feedParams'
import { fetchAllPages } from './paging'

const parse = (q: string) => parseFeedParams(new URLSearchParams(q))

describe('feed URL state', () => {
  it('defaults to rating changes across everything', () => {
    expect(parse('')).toEqual({ view: 'changes', ticker: null, sector: null, page: 1 })
    expect(feedSearchParams(parse('')).toString()).toBe('')
  })

  it('round-trips filters and rejects bad values', () => {
    const state = parse('show=Downgrade&ticker=brk.b&sector=Energy&page=2')
    expect(state).toEqual({ view: 'Downgrade', ticker: 'BRK.B', sector: 'Energy', page: 2 })
    expect(parseFeedParams(feedSearchParams(state))).toEqual(state)
    expect(parse('show=bogus&ticker=1abc&page=x')).toEqual({ view: 'changes', ticker: null, sector: null, page: 1 })
  })

  it('maps views onto the API filters', () => {
    expect(feedFilters(parse(''))).toMatchObject({ actions: [], includeAll: false })
    expect(feedFilters(parse('show=Initiate'))).toMatchObject({ actions: ['Initiate'], includeAll: false })
    expect(feedFilters(parse('show=all'))).toMatchObject({ actions: [], includeAll: true })
  })
})

describe('fetchAllPages', () => {
  const pages = (total: number) => async (page: number) => ({ items: [page], totalPages: total })

  it('stops at the last page', async () => {
    expect(await fetchAllPages(pages(3))).toEqual({ items: [1, 2, 3], truncated: false })
    expect(await fetchAllPages(pages(0))).toEqual({ items: [1], truncated: false })
  })

  it('stops at the cap and says so', async () => {
    expect(await fetchAllPages(pages(9), 2)).toEqual({ items: [1, 2], truncated: true })
  })
})
