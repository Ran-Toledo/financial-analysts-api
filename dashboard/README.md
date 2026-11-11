# Dashboard

React 19 + TypeScript, built with Vite. Routing with React Router, server state with TanStack Query.

## Scripts

| Command | What it does |
|---|---|
| `npm run dev` | Dev server on http://localhost:5173 with hot reload. `/api` is forwarded to the API on http://localhost:5076, so start the API too. |
| `npm run build` | Type-checks, then writes the production build into `../src/FinancialAnalystsAPI/wwwroot`. The API serves it at `/`, and client-side routes fall back to `index.html`. |
| `npm test` | Vitest unit tests for the helpers in `src/lib`. |
| `npm run lint` | oxlint. |
| `npm run gen:api` | Regenerates `src/api/schema.d.ts` from the running API's Swagger document. Run it after changing a DTO or endpoint. |

`gen:api` runs openapi-typescript through a pinned `npx` because it needs TypeScript 5's compiler API, while the app builds with TypeScript 7.

## Layout

| Path | Contents |
|---|---|
| `src/api/` | Typed API client (`openapi-fetch`), generated schema, and query hooks |
| `src/lib/` | Pure helpers: formatting, URL ↔ filter state, sparkline geometry, error messages |
| `src/components/` | Layout, sparkline, empty/error states |
| `src/pages/` | One component per route |

## Pages

- `/`: leaderboard of firms or analysts (`?view=analysts`), with horizon, sector and firm filters, sorting and paging in the query string so a view can be shared.
