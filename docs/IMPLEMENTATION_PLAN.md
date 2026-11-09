# Implementation Plan: Phases 1–4

This plan covers Phases 1–4 of [TODO.md](../TODO.md). The goal is to track real Wall Street analysts and score how accurate their calls turn out to be.

**Where the code is today:** a .NET 9 Web API at the repo root. It has one entity (`FinancialAnalyst`: Name, Expertise, manual `Rating`), one CRUD controller, SQLite through EF Core, and Swashbuckle. There are no tests, no DTOs and no external data.

Each step below is sized to be one reviewable PR. Tests from "Ongoing: Engineering basics" are written alongside each step, not afterwards.

---

## Phase 0: Prep (one PR, before Phase 1)

The repo root holds the `.csproj`, and SDK-style projects compile every `**/*.cs` beneath their folder. A `tests/` folder added at the root would therefore get compiled into the API. Restructure first:

```
FinancialAnalystsAPI.sln
src/FinancialAnalystsAPI/         <- current project moved here (Controllers, Data, Models, Migrations, Program.cs…)
tests/FinancialAnalystsAPI.Tests/ <- xUnit; unit + WebApplicationFactory integration tests
dashboard/                        <- Phase 4 (Vite app)
docs/
```

- Use `git mv` so file history is kept, then update the `.sln` and the README run commands.
- Add the xUnit project with `Microsoft.AspNetCore.Mvc.Testing`, and add `public partial class Program;` so integration tests can reach `Program`.
- Fix the nullable warnings in `FinancialAnalyst` (`string Name` is non-nullable but never initialised).

---

## Phase 1: Data foundation

### 1.1 Choose the provider (spike, no merged code)

| | Financial Modeling Prep | Finnhub |
|---|---|---|
| Free tier | ~250 calls/day, EOD prices, some US symbols | 60 calls/min, but **price targets and upgrades/downgrades are premium-only** |
| Analyst identity per call | Yes: analyst name and firm on price-target / ratings endpoints | Firm only, no analyst name |
| Historical prices | Yes (EOD) | Candles are limited on the free plan |

**Recommendation: FMP.** It is the only one of the two that exposes *individual analyst* calls, and the whole product depends on that. Finnhub's free tier gives only aggregate recommendation trends.

Before writing the provider:
1. Get a free FMP key and call the `stable` price-target, grades and historical-EOD endpoints for a couple of tickers. Confirm which are available on the free plan; third-party sources disagree about this.
2. Save sample JSON responses into `tests/.../Fixtures/fmp/`. They become test fixtures and the fake provider's data.
3. If analyst-level endpoints turn out to be paid-only, decide whether to pay for the cheapest FMP tier that has them. The abstraction in 1.2 keeps that decision isolated.

**Planning constraint:** 250 calls/day means you track a *configured universe* of tickers (start with about 25, plus `SPY` as a benchmark), not the whole market.

### 1.2 Provider abstraction

```csharp
public interface IAnalystDataProvider
{
    Task<StockProfile?> GetStockProfileAsync(string ticker, CancellationToken ct);
    Task<IReadOnlyList<ProviderRating>> GetRatingsAsync(string ticker, DateOnly since, CancellationToken ct);
    Task<IReadOnlyList<ProviderPrice>> GetDailyPricesAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct);
}
```

- `Providers/Fmp/FmpAnalystDataProvider.cs` is a typed `HttpClient`. Add `Microsoft.Extensions.Http.Resilience` with `AddStandardResilienceHandler()` for retries and backoff, and a client-side limiter so the job cannot burn through the daily quota.
- Provider DTOs (`ProviderRating`, …) are plain records that stay separate from EF entities. All mapping and normalisation happens in the ingestion layer.
- `FakeAnalystDataProvider` serves the saved fixtures. It is selected with `DataProvider:Name = "Fake"`, so the app and tests run without a key.

### 1.3 Secrets and configuration

- Run `dotnet user-secrets init`, then `dotnet user-secrets set "Fmp:ApiKey" "<key>"` locally. In deployment set the `Fmp__ApiKey` environment variable.
- Bind `FmpOptions` and `IngestionOptions` with `AddOptions<T>().BindConfiguration(...).ValidateDataAnnotations().ValidateOnStart()`. A missing key then fails at startup, not on the first job run.
- Document both settings in the README. Never put the key in `appsettings*.json`.

### 1.4 Data model

```
Firm          Id, Name (unique), ExternalId?
Analyst       Id, Name, FirmId → Firm, ExternalId? (unique when present), Rating (double?, kept until Phase 3)
Stock         Id, Ticker (unique), Name, Sector, Industry, IsTracked
Rating        Id, AnalystId? → Analyst, FirmId → Firm, StockId → Stock, Date (DateOnly),
              Action (enum: Initiate, Upgrade, Downgrade, Maintain, Reiterate, TargetRaise, TargetLower),
              RatingFrom?, RatingTo (raw firm strings, e.g. "Overweight"),
              Category (enum: Buy, Hold, Sell; normalised from RatingTo),
              PriceTargetFrom?, PriceTargetTo?, PriceAtCall?,
              Source, SourceKey (unique, used for idempotent upserts)
PriceSnapshot StockId → Stock, Date, Close, AdjClose; PK (StockId, Date)
IngestionRun  Id, Kind, StartedAt, FinishedAt, Status, ItemsAdded, Error
```

Notes:
- **"Sectors covered" is derived, not stored.** It is the distinct `Stock.Sector` values across an analyst's ratings. Storing it would only duplicate data that can drift.
- **`AnalystId` is nullable** because some rows only name a firm. Those rows still count toward firm leaderboards and stock consensus.
- **Rating normalisation:** a static map turns firm vocabulary into a `Category` (Outperform/Overweight/Strong Buy → Buy; Market Perform/Neutral/Equal-Weight → Hold; …). Strings the map doesn't know are logged and stored as `Hold`, and the map gets unit tests.
- **Use `double`, not `decimal`, for prices.** EF Core on SQLite cannot translate `Sum`/`Average`/`Min`/`Max` or `ORDER BY` over `decimal`, and Phases 2 and 3 depend on those aggregates.
- Indexes: `Rating(StockId, Date)`, `Rating(AnalystId, Date)`, `Rating(Date)` for the feed, and `Stock(Sector)`.
- Configure everything in `IEntityTypeConfiguration<T>` classes, applied with `ApplyConfigurationsFromAssembly`.
- Migration: rename `FinancialAnalyst` to `Analyst`, drop `Expertise`, and make `Rating` nullable. Dev data is disposable, so regenerating `analysts.db` is acceptable.
- The current POST/PUT/DELETE endpoints write analysts by hand, which conflicts with ingested data. Remove them in Phase 2 when the controllers are rewritten.

### 1.5 Scheduled ingestion

**Use a `BackgroundService`, not Hangfire.** There are only two daily jobs on a single instance. Hangfire adds a storage schema and a dashboard that this doesn't need yet.

- `IngestionWorker : BackgroundService` uses a `PeriodicTimer` and runs once a day after US market close (configurable, e.g. 22:30 UTC). It creates a scope per run through `IServiceScopeFactory`, because `DbContext` is scoped.
- The work itself lives in `IngestionService`, so tests and an admin endpoint can call it directly:
  1. Upsert `Stock` profiles for the tracked tickers from `Tracking:Tickers` in config.
  2. For each ticker, fetch ratings since the last successful `IngestionRun`. Upsert `Firm`/`Analyst` and insert `Rating` rows, deduplicated by `SourceKey`.
  3. Fetch daily prices since the last stored date, and fill in `Rating.PriceAtCall` from the close on or before the call date.
- **Backfill mode** (`IngestionService.BackfillAsync(years: 3)`) runs once. Without history, Phase 3 has nothing to score for 12 months.
- Every run writes an `IngestionRun` row. One ticker failing does not stop the others.

**Done when:** with the fake provider, a test runs ingestion twice and gets the same row counts (the job is idempotent). With a real key, `analysts.db` holds ratings and prices for the tracked universe.

---

## Phase 2: Useful API

### 2.1 DTOs and the controller layout
Stop returning EF entities. Use `record` DTOs and project queries with `Select` so that only the columns needed are read.

| Route | Purpose |
|---|---|
| `GET /api/stocks` | List; filter `sector`, `search`; sort `ticker`, `name`, `ratingCount` |
| `GET /api/stocks/{ticker}` | Profile + latest close |
| `GET /api/stocks/{ticker}/consensus` | Current consensus |
| `GET /api/stocks/{ticker}/consensus/history?interval=month` | Consensus over time |
| `GET /api/stocks/{ticker}/ratings` | Ratings on the stock |
| `GET /api/stocks/{ticker}/prices?from&to` | Price series (for charts) |
| `GET /api/analysts`, `/{id}`, `/{id}/ratings` | Analyst list, profile and calls |
| `GET /api/firms`, `/{id}` | Firm list and detail |
| `GET /api/feed` | Upgrade/downgrade feed; filter `ticker`, `sector`, `action`, `since` |
| `POST /api/admin/ingestion/run` | Trigger ingestion (API key with admin role) |

### 2.2 Consensus
- **Current:** take the latest rating per analyst (or per firm when there is no analyst) for the stock within a lookback window (default 90 days, configurable). Return Buy/Hold/Sell counts and percentages, mean/median/high/low of `PriceTargetTo`, the latest close, and implied upside.
- **History:** compute the same thing as of each month-end. Move it into a service (`ConsensusService`) so Phase 4's gauge and the history chart share one definition.

### 2.3 Pagination, filtering, sorting
- `PagedQuery` (`page` ≥ 1, `pageSize` 1–100, default 25; `sort` and `dir`) returns `PagedResult<T> { Items, Page, PageSize, TotalCount }`.
- Sorting uses a **whitelist**: each endpoint maps allowed sort keys to expressions. Never build expressions from raw strings.
- A small `IQueryable` extension (`.ApplyPaging(query)`) keeps controllers thin.

### 2.4 Validation
- Use DataAnnotations on query and request models. `[ApiController]` already returns 400 automatically. Add `builder.Services.AddProblemDetails()` so every error comes back as RFC 7807.
- Validate tickers with a regex (`^[A-Z.\-]{1,10}$`) and date ranges with `from <= to` and a capped span. Use FluentValidation only if rules outgrow attributes.

### 2.5 Caching and rate limiting
- **Output caching** (`AddOutputCache`): consensus, leaderboards and stock pages are cached for an hour and tagged `market-data`. When ingestion finishes it calls `IOutputCacheStore.EvictByTagAsync("market-data")`, so cached data is never older than the last ingest.
- **Rate limiting** (`AddRateLimiter`): a fixed window partitioned by API key, with a fallback partition by IP. Anonymous callers get a lower limit and responses past the limit return `429` with `Retry-After`.
- Middleware order: `UseRouting` → `UseAuthentication` → `UseAuthorization` → `UseRateLimiter` → `UseOutputCache` → endpoints.

### 2.6 API-key authentication
- Add a custom `AuthenticationHandler<ApiKeyOptions>` that reads `X-Api-Key`. Store keys **hashed** (SHA-256) in an `ApiClient` table (Name, KeyHash, Role, IsActive, CreatedAt). Seed keys with a small CLI command or a dev-only seeder.
- Policies: `ReadAccess` (any valid key) and `Admin` (the ingestion trigger).
- Add a Swagger `SecurityDefinition` so the "Authorize" button works.
- **Decision needed:** whether reads require a key. The dashboard is a browser app, so a key embedded in it is public. Recommendation: GET endpoints allow anonymous access with the stricter IP limit, a key raises the limit, and admin endpoints require the `Admin` role.

**Done when:** integration tests (`WebApplicationFactory`, SQLite in-memory, fake provider) cover paging bounds, invalid sort keys (400), consensus maths, 401/403 for admin endpoints, and 429 after the limit.

---

## Phase 3: Scoring

### 3.1 Definitions
These should be agreed before writing code, because they determine every number on the leaderboard.

- **A call** is a `Rating` that sets a new `Category` or a new `PriceTargetTo` for that analyst and stock. Pure "Maintain/Reiterate" rows with no change are skipped, so analysts who reiterate often aren't over-counted.
- **Horizons:** 3, 6 and 12 months, measured in calendar months from the call date. The horizon price is the last close on or before that date. A call is **pending** until its horizon has passed. Only matured calls are scored.
- **Per call and horizon:**
  - `Return = P_h / P_0 − 1`, where `P_0` is `PriceAtCall`.
  - `ExcessReturn = Return − SPY return` over the same window. This needs `SPY` to be ingested.
  - `DirectionalReturn`: `+ExcessReturn` for Buy, `−ExcessReturn` for Sell. Hold calls are scored as a hit when `|ExcessReturn| < 5%`, and the band is configurable.
  - `DirectionHit = DirectionalReturn > 0`.
  - `TargetHit`: the close reached `PriceTargetTo` at any point within the horizon, in the direction of the call.
  - `TargetError = |P_h − Target| / P_0`.

### 3.2 Engine
- `ScoringEngine` is **pure**: inputs are calls and price series, outputs are `CallOutcome` records, with no EF dependency. That makes it easy to cover thoroughly with xUnit cases (gaps, weekends, splits via `AdjClose`, missing prices).
- `CallOutcome` table: `(RatingId, HorizonMonths)` PK, `PriceAtHorizon`, `Return`, `ExcessReturn`, `DirectionHit`, `TargetHit?`, `TargetError?`, `EvaluatedAt`.
- `ScoringService` runs after each price ingestion and **only scores newly matured calls**, which keeps it incremental.

### 3.3 Aggregates and the computed score
- `AnalystScore`: `(AnalystId, HorizonMonths, Sector?)`, where a null sector means overall. It holds `CallCount`, `HitRate`, `AvgReturn`, `AvgExcessReturn`, `TargetHitRate`, `AvgTargetError`, `Score`, `ComputedAt`. There is a matching `FirmScore`.
- **Score formula:** a 0–100 blend of hit rate and average excess return, **shrunk toward the population mean** for small samples: `adj = (n·x + k·μ)/(n + k)`, with `k ≈ 10`. Without shrinkage, an analyst with 3 lucky calls tops the board. Leaderboards also require `minCalls` (default 10).
- `AnalystScoreSnapshot`: `(AnalystId, HorizonMonths, AsOf)` stores the score at each month-end. Phase 4's trend sparklines need it, so add it here.
- **Replace the manual `Rating`:** add a migration that drops `Analyst.Rating`. `AnalystDto` then exposes `Score` (12-month horizon by default) and `CallCount`. Any remaining write endpoint for analysts goes too.

### 3.4 Leaderboards
- `GET /api/leaderboards/analysts?horizon=12&sector=&firmId=&minCalls=10` plus paging.
- `GET /api/leaderboards/firms?horizon=12&sector=`
- `GET /api/analysts/{id}/scores` returns the per-horizon breakdown and the snapshot history.
- All of these are output-cached with the `market-data` tag.

**Done when:** engine unit tests pass on hand-calculated cases, and leaderboards over backfilled data look plausible when spot-checked against a few known calls.

---

## Phase 4: Dashboard

### 4.1 Stack
**Recommendation: React + Vite + TypeScript.** The charting needs (price series with markers, sparklines, scatter plots) are much better served there than in Blazor.

- Routing: React Router. Server state: TanStack Query.
- Charts: **TradingView Lightweight Charts** for the price chart with target and rating markers (Apache-2.0, attribution required). **Recharts** for sparklines, scatter plots and the gauge.
- API types are generated from the Swagger document with `openapi-typescript`, so the client never drifts from the API.
- Dev: the Vite proxy sends `/api` to `http://localhost:5076`, so no CORS is needed. Prod: `npm run build` outputs to the API's `wwwroot`, and `MapFallbackToFile("index.html")` serves it as one deployable unit (and same-origin).

### 4.2 Pages
1. **Leaderboard** (`/`): sortable, paginated analyst table (rank, analyst, firm, score, hit rate, avg excess return, call count) with a 12-month score sparkline from `AnalystScoreSnapshot`. Horizon/sector/firm filters live in the URL query string so the view can be shared.
2. **Analyst profile** (`/analysts/:id`): header with scores per horizon. A scatter plot of call date against excess return, coloured by Buy/Hold/Sell. A table of past calls with outcomes, and pending calls marked as pending.
3. **Stock page** (`/stocks/:ticker`): price chart with markers for rating changes and target lines, a consensus gauge (Buy/Hold/Sell split plus mean target against the last close), and a consensus-history chart.
4. **Live feed** (`/feed`): rating changes newest-first with ticker/sector/action filters. It polls with `refetchInterval` (60 s). Data changes once a day, so SignalR isn't worth it yet.

### 4.3 Quality bar
- Loading, empty and error states on every page. Layout works at phone width.
- A Vitest test for the formatting and helper functions, and a Playwright smoke test that loads each page against the fake-provider API.

---

## Order of work

| # | PR | Depends on |
|---|---|---|
| 0 | Restructure into `src/` + `tests/` | — |
| 1 | FMP spike + saved fixtures (not merged) | — |
| 2 | Data model + migration | 0 |
| 3 | Provider abstraction, FMP + fake provider, options/secrets | 1, 2 |
| 4 | Ingestion service + worker + backfill | 3 |
| 5 | DTOs, read controllers, paging/sorting/validation | 2 |
| 6 | Consensus service + endpoints | 5 |
| 7 | Feed endpoint | 5 |
| 8 | API-key auth, rate limiting, output caching | 5 |
| 9 | Scoring engine + `CallOutcome` | 4 |
| 10 | Aggregates, snapshots, drop manual `Rating` | 9 |
| 11 | Leaderboard endpoints | 10, 8 |
| 12 | Dashboard scaffold + Leaderboard page | 11 |
| 13 | Analyst profile, Stock page, Feed page | 12, 6, 7 |

PRs 5–8 can run in parallel with 3–4 because the fake provider and seeded test data unblock them.

## Open decisions

1. **Provider plan:** whether FMP's free key returns analyst-level data. Settle this with the spike in 1.1 before PR 3.
2. **Anonymous reads:** recommended yes, with a stricter rate limit (2.6).
3. **Scoring definitions:** the Hold band (5%), SPY as the benchmark, the shrinkage constant, and the minimum number of calls (3.1, 3.3).
4. **Dashboard stack:** React + Vite (recommended) or Blazor.
