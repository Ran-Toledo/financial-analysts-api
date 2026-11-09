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

## Test

```bash
dotnet test
```

Tests use in-memory SQLite and the fake provider, so they don't touch `analysts.db` or the network.

## Endpoints

| Method | Route | Description |
|---|---|---|
| GET | `/api/analysts` | List analysts with firm and rating count |
| GET | `/api/analysts/{id}` | Get one analyst |

Analysts are created by ingestion, so there are no write endpoints. Stocks, consensus, the activity feed and paging arrive in Phase 2.
