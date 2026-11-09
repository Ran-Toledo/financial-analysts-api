# TODO

Goal: track real Wall Street analysts and score how accurate their calls turn out to be.

## Phase 1: Data foundation
- [x] Choose a data provider (Financial Modeling Prep or Finnhub; check current free tiers): FMP, since Finnhub's targets and upgrades are premium-only
  - [x] Verify with a real FMP key which endpoints the free tier returns: profiles, prices (some symbols) and firm-level `grades` yes; `price-target-news` (analyst names, targets) no
  - [ ] Decide how to get analyst names and price targets: a paid FMP plan, firm-level scoring only, or another provider
    - `price-target-news` is no longer in FMP's docs. Its replacement is the TipRanks-sourced dataset (`tipranks-search`, `tipranks-pit-symbol`, …), which has analyst IDs, targets, actions and 3 years of history
    - Next step: try `tipranks-search?symbol=AAPL` once with the free key (via "Explore Endpoint" in FMP's docs). If it returns rows, switch the FMP provider to it
    - Free plan only covers ~87 symbols; CRM, PG and HD from the default list aren't among them
  - [ ] Replace the hand-written success-response test fixtures with saved real responses
- [x] Add an `IAnalystDataProvider` abstraction and a first implementation (FMP, plus an offline fake)
- [x] Store API keys with user-secrets locally and environment variables in deployment
- [x] Expand the data model:
  - [x] Analyst (name, firm, sectors covered)
  - [x] Firm
  - [x] Stock (ticker, name, sector)
  - [x] Rating (analyst, stock, date, action, rating, price target)
  - [x] PriceSnapshot (daily closing prices)
- [x] Add a scheduled ingestion job (`BackgroundService` or Hangfire) for ratings, price targets, and prices

## Phase 2: Useful API
- [x] Consensus per stock (buy/hold/sell split; average, high, and low targets; history). Target stats stay null until a provider supplies targets
- [x] Activity feed of upgrades and downgrades, filterable by ticker and sector
- [x] Pagination, filtering, and sorting on list endpoints
- [x] Input validation
- [x] Response caching and rate limiting
- [x] API-key authentication

## Phase 3: Scoring
- [ ] Accuracy engine: compare price targets with the actual price at 3, 6, and 12 months
- [ ] Per-analyst hit rate and average return; replace the manual `Rating` with the computed score
- [ ] Leaderboards: overall, by sector, and by firm

## Phase 4: Dashboard (React + Vite or Blazor)
- [ ] Leaderboard page: sortable analyst table with scores and trend sparklines
- [ ] Analyst profile: past calls plotted against actual stock performance
- [ ] Stock page: price chart with targets and rating changes overlaid, plus a consensus gauge
- [ ] Live feed page of rating changes

## Phase 5: Alerts and deployment
- [ ] Watchlists
- [ ] Alerts by email or webhook (e.g. a top-ranked analyst changes a rating on a watched stock)
- [ ] Docker
- [ ] Deployment

## Ongoing: Engineering basics
- [ ] Unit tests (xUnit)
- [ ] Integration tests (`WebApplicationFactory`)
- [ ] GitHub Actions CI
- [ ] Optionally move from SQLite to PostgreSQL
