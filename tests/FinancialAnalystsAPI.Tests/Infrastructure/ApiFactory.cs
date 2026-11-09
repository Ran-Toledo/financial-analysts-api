using FinancialAnalystsAPI.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAnalystsAPI.Tests.Infrastructure;

/// <summary>
/// Hosts the API in memory against a private, shared-cache in-memory SQLite database.
/// The database lives as long as <see cref="_keepAlive"/> stays open, i.e. for the factory's lifetime.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString =
        $"Data Source=file:test-{Guid.NewGuid():N}?mode=memory&cache=shared";

    private readonly SqliteConnection _keepAlive;

    public ApiFactory()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:AnalystsDb", _connectionString);
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);

        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AnalystsDbContext>().Database.Migrate();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _keepAlive.Dispose();
    }
}
