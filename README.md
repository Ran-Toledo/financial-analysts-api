# FinancialAnalystsAPI

An ASP.NET Core (.NET 9) Web API that tracks Wall Street analysts' ratings and price targets, stored in SQLite via Entity Framework Core. See [TODO.md](TODO.md) for the roadmap and [docs/IMPLEMENTATION_PLAN.md](docs/IMPLEMENTATION_PLAN.md) for the design.

## Layout

| Path | Contents |
|---|---|
| `src/FinancialAnalystsAPI/` | The Web API project |
| `tests/FinancialAnalystsAPI.Tests/` | xUnit unit and integration tests (`WebApplicationFactory`) |
| `docs/` | Design notes and the implementation plan |

## Run

From the repo root:

```bash
dotnet ef database update --project src/FinancialAnalystsAPI   # create or upgrade analysts.db
dotnet run --project src/FinancialAnalystsAPI -- ingest           # load data once
dotnet run --project src/FinancialAnalystsAPI                     # start the API
```

Swagger UI is available at `/swagger` in Development. The connection string is `ConnectionStrings:AnalystsDb` in `src/FinancialAnalystsAPI/appsettings.json`; with the commands above, `analysts.db` lives in `src/FinancialAnalystsAPI/`.

> Upgrading an existing `analysts.db`: the `DataFoundation` migration deletes the rows from the old hand-entered analysts table, because analysts now come from ingestion.

## Data provider

`DataProvider:Name` selects where data comes from:

| Value | Used by default in | What it does |
|---|---|---|
| `Fake` | Development, tests | Deterministic synthetic prices and ratings from fictional firms and analysts. No key, no network. |
| `Fmp` | Everything else | [Financial Modeling Prep](https://site.financialmodelingprep.com/developer/docs) `stable` API. Needs an API key. |

The FMP key never goes in `appsettings*.json`:

```bash
dotnet user-secrets set "Fmp:ApiKey" "<your key>" --project src/FinancialAnalystsAPI
dotnet user-secrets set "DataProvider:Name" "Fmp" --project src/FinancialAnalystsAPI
```

In deployment, set the `Fmp__ApiKey` environment variable instead. The app refuses to start when `Fmp` is selected and no key is set.

`Fmp:DailyCallBudget` (default 240) caps calls per UTC day, below the free tier's 250. A daily run uses about 3–5 calls per ticker.

**What FMP's free tier returns** (checked November 2026):

| Data | Endpoint | Free tier |
|---|---|---|
| Profiles | `profile` | Yes |
| Daily prices | `historical-price-eod/full`, `dividend-adjusted` | Yes, but only for some symbols (not CRM, PG or HD from the default list) |
| Firm rating changes | `grades` | Yes: about 3 years, roughly 90 firms |
| Analyst names and price targets | `price-target-news` | **No** (402 "Restricted Endpoint") |

When `price-target-news` is refused, ingestion keeps going with firm-level grades only and skips that endpoint for 24 hours. An invalid key (401/403) stops the run at the first ticker.

## Ingestion

- **Tracked universe:** `Tracking:Tickers` (25 large caps by default) plus `Tracking:BenchmarkTicker` (`SPY`).
- **Schedule:** a background job runs daily at `Ingestion:RunAtUtc` (default 22:30 UTC, after US close). Set `Ingestion:Enabled` to `false` to turn it off, or `Ingestion:RunOnStartup` to `true` to also run at startup.
- **On demand:** `dotnet run --project src/FinancialAnalystsAPI -- ingest` runs once and exits. Add `--backfill` to re-fetch `Ingestion:BackfillYears` (default 3) of history for every ticker.
- **Each run:** fetches profiles (new tickers only), daily prices since the last stored day, and ratings since the newest stored rating minus `Ingestion:RatingsOverlapDays`. A ticker's first run loads the full backfill window.
- **Idempotent:** re-running adds nothing already stored. Every run is recorded in the `IngestionRuns` table.
- **From the API:** `POST /api/admin/ingestion/run` (admin key) starts a run in the background. Runs never overlap within the web process, and cached responses are cleared when a run finishes.

## Test

```bash
dotnet test
```

Tests use in-memory SQLite and the fake provider, so they don't touch `analysts.db` or the network.

## Endpoints

Full reference with parameters: Swagger UI at `/swagger` (Development).

| Method | Route | Description |
|---|---|---|
| GET | `/api/stocks` | Tracked stocks with last close and rating count. Filter `sector`, `search`; sort `ticker`, `name`, `sector`, `ratingCount` |
| GET | `/api/stocks/sectors` | Distinct sectors, for filter menus |
| GET | `/api/stocks/{ticker}` | One stock |
| GET | `/api/stocks/{ticker}/ratings` | Ratings on the stock. Filter `from`, `to`, `action`, `category` |
| GET | `/api/stocks/{ticker}/prices` | Daily closes, `from`/`to` (default: last year; max 10 years) |
| GET | `/api/stocks/{ticker}/consensus` | Buy/Hold/Sell split, score, and price-target summary. `asOf`, `lookbackDays` (default 90) |
| GET | `/api/stocks/{ticker}/consensus/history` | Consensus at each month-end. `months` (default 12) |
| GET | `/api/analysts`, `/api/analysts/{id}`, `/api/analysts/{id}/ratings` | Analysts and their calls (empty on FMP's free tier) |
| GET | `/api/firms`, `/api/firms/{id}`, `/api/firms/{id}/ratings` | Research firms and their calls |
| GET | `/api/feed` | Upgrades, downgrades and initiations across tracked stocks, newest first. Filter `ticker`, `sector`, `firmId`, `analystId`, `from`, `to`, `action`; `includeAll=true` adds maintained calls |
| GET | `/api/admin/ingestion/runs`, `/runs/{id}` | Ingestion history (admin) |
| POST | `/api/admin/ingestion/run?backfill=false` | Start an ingestion run (admin) |

**Conventions**
- Lists take `page` (from 1), `pageSize` (1–100, default 25), `sort` and `dir` (`asc`/`desc`), and return `{ items, page, pageSize, totalCount, totalPages }`.
- Errors are RFC 7807 `application/problem+json`. Invalid input returns 400 with per-field messages, including the allowed `sort` values.
- Enums are strings: `action` is `Initiate`, `Upgrade`, `Downgrade`, `Maintain`, `Reiterate`, `TargetRaise` or `TargetLower`; `category` is `Buy`, `Hold` or `Sell`.
- **Consensus:** each contributor's latest call within the window counts once. A contributor is an analyst, or the firm when the provider names no analyst. `score` is the mean of Buy = 1, Hold = 2, Sell = 3, and `rating` is Buy when the score is at most 1.5, Sell when it is at least 2.5, otherwise Hold. `targets` is null when no call in the window has a price target.

## API keys and limits

Reads are open to anonymous callers. An API key in the `X-Api-Key` header raises the rate limit, and an **admin** key is needed for `/api/admin`. A key that doesn't match returns 401 on every endpoint rather than silently falling back to anonymous.

```bash
dotnet run --project src/FinancialAnalystsAPI -- apikey create my-app            # reader key
dotnet run --project src/FinancialAnalystsAPI -- apikey create ops --admin       # admin key
dotnet run --project src/FinancialAnalystsAPI -- apikey list
dotnet run --project src/FinancialAnalystsAPI -- apikey revoke my-app
```

The key is printed once. Only its SHA-256 hash is stored, and a revoked key stops working within a minute.

| Setting | Default | Meaning |
|---|---|---|
| `ApiLimits:AnonymousPerMinute` | 60 | Requests per minute per IP without a key (only `/api` paths) |
| `ApiLimits:ApiKeyPerMinute` | 600 | Requests per minute per key |
| `ApiLimits:CacheMinutes` | 60 | How long read responses are cached; ingestion runs in the web process clear the cache when they finish |

Over the limit, the API returns 429 with a `Retry-After` header. Runs from the separate `ingest` command don't clear the web server's cache, so the API can show older data for up to `CacheMinutes` after one.
