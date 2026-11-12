# Dashboard

React 19 + TypeScript, built with Vite. Routing with React Router, server state with TanStack Query. Charts: TradingView Lightweight Charts for the price chart, Recharts for the rest.

## Scripts

| Command | What it does |
|---|---|
| `npm run dev` | Dev server on http://localhost:5173 with hot reload. `/api` is forwarded to the API on http://localhost:5076, so start the API too. |
| `npm run build` | Type-checks, then writes the production build into `../src/FinancialAnalystsAPI/wwwroot`. The API serves it at `/`, and client-side routes fall back to `index.html`. |
| `npm test` | Vitest unit tests for the helpers in `src/lib`. |
| `npm run test:e2e` | Playwright smoke test of every page against a fake-data API (see below). |
| `npm run lint` | oxlint. |
| `npm run gen:api` | Regenerates `src/api/schema.d.ts` from the running API's Swagger document. Run it after changing a DTO or endpoint. |

`gen:api` runs openapi-typescript through a pinned `npx` because it needs TypeScript 5's compiler API, while the app builds with TypeScript 7.

## Layout

| Path | Contents |
|---|---|
| `src/api/` | Typed API client (`openapi-fetch`), generated schema, and query hooks |
| `src/lib/` | Pure helpers (unit-tested): formatting, dates, URL ↔ filter state, chart geometry, consensus math, error messages |
| `e2e/` | Playwright smoke test and the script that runs the fake-data API |
| `src/components/` | Layout, charts, table cells, empty/error states |
| `src/pages/` | One component per route |

## Pages

| Route | What it shows |
|---|---|
| `/` | Leaderboard of firms or analysts (`?view=analysts`) with horizon, sector and firm filters, sorting and paging, all in the query string. |
| `/firms/:id`, `/analysts/:id` | Scorecard: score, rank, hit rate and excess return per horizon; score over time; a scatter of every scored call (date against excess return, colored Buy/Hold/Sell, filled when right); the scored calls; and calls too recent to score. |
| `/stocks` | The tracked universe, searchable and sortable. |
| `/stocks/:ticker` | Price chart with rating-change markers and the month-end mean target; today's Buy/Hold/Sell split and price-target range; consensus by month; every call. |
| `/feed` | Upgrades, downgrades and initiations, newest first, filterable by action, stock and sector. Checks for new calls every minute. |

Pages other than the leaderboard load on demand, so the chart libraries only download when they're needed.

## Smoke tests

`npm run test:e2e` starts two servers and runs `e2e/smoke.spec.ts` against them:

- **API on :5077** (`e2e/fake-api.mjs`): the fake provider with its own database in `e2e/.data/fake.db`. The first run creates and fills it, which takes a few minutes; delete the folder to rebuild it. Stop any `dotnet run` of the API first, because a running API locks the build output.
- **Vite on :5179**, with `/api` forwarded to :5077.

On Windows the tests drive the installed Microsoft Edge. Elsewhere, run `npx playwright install chromium` once (or set `PW_CHANNEL=chrome`).
