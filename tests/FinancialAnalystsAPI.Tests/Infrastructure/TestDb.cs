using FinancialAnalystsAPI.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinancialAnalystsAPI.Tests.Infrastructure;

/// <summary>A migrated in-memory SQLite database that lives until disposed. Create a fresh context per unit of work.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public TestDb()
    {
        _connection.Open();
        using var db = CreateContext();
        db.Database.Migrate();
    }

    public AnalystsDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AnalystsDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
