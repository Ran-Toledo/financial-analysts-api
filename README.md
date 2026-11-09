# FinancialAnalystsAPI

A small ASP.NET Core (.NET 9) Web API for managing financial analysts, stored in SQLite via Entity Framework Core.

## Layout

| Path | Contents |
|---|---|
| `src/FinancialAnalystsAPI/` | The Web API project |
| `tests/FinancialAnalystsAPI.Tests/` | xUnit unit and integration tests (`WebApplicationFactory`) |
| `docs/` | Design notes and the implementation plan |

## Run

From the repo root:

```bash
dotnet ef database update --project src/FinancialAnalystsAPI   # create analysts.db
dotnet run --project src/FinancialAnalystsAPI
```

Swagger UI is available at `/swagger` in Development. The connection string is `ConnectionStrings:AnalystsDb` in `src/FinancialAnalystsAPI/appsettings.json`; with the commands above, `analysts.db` lives in `src/FinancialAnalystsAPI/`.

## Test

```bash
dotnet test
```

Integration tests run against an in-memory SQLite database, so they don't touch `analysts.db`.

## Endpoints

| Method | Route | Description |
|---|---|---|
| GET | `/api/FinancialAnalysts` | List all analysts |
| GET | `/api/FinancialAnalysts/{id}` | Get one analyst |
| POST | `/api/FinancialAnalysts` | Create an analyst |
| PUT | `/api/FinancialAnalysts/{id}` | Update an analyst |
| DELETE | `/api/FinancialAnalysts/{id}` | Delete an analyst |
