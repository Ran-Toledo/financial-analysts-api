# FinancialAnalystsAPI

A small ASP.NET Core (.NET 9) Web API for managing financial analysts, stored in SQLite via Entity Framework Core.

## Run

```bash
dotnet ef database update   # create analysts.db
dotnet run
```

Swagger UI is available at `/swagger` in Development. The connection string is `ConnectionStrings:AnalystsDb` in `appsettings.json`.

## Endpoints

| Method | Route | Description |
|---|---|---|
| GET | `/api/FinancialAnalysts` | List all analysts |
| GET | `/api/FinancialAnalysts/{id}` | Get one analyst |
| POST | `/api/FinancialAnalysts` | Create an analyst |
| PUT | `/api/FinancialAnalysts/{id}` | Update an analyst |
| DELETE | `/api/FinancialAnalysts/{id}` | Delete an analyst |
