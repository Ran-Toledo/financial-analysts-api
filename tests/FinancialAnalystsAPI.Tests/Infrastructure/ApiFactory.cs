using FinancialAnalystsAPI.Auth;
using FinancialAnalystsAPI.Data;
using FinancialAnalystsAPI.Ingestion;
using FinancialAnalystsAPI.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace FinancialAnalystsAPI.Tests.Infrastructure;

/// <summary>
/// Hosts the API in memory against a private, shared-cache in-memory SQLite database, with the fake provider,
/// a fixed clock (2026-11-09) and a small tracked universe (AAPL, MSFT + SPY).
/// The database lives as long as <see cref="_keepAlive"/> stays open, i.e. for the factory's lifetime.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset Now = new(2026, 11, 9, 23, 0, 0, TimeSpan.Zero);

    private readonly string _connectionString =
        $"Data Source=file:test-{Guid.NewGuid():N}?mode=memory&cache=shared";

    private readonly SqliteConnection _keepAlive;
    private readonly SemaphoreSlim _initGate = new(1, 1);
    private bool _initialized;

    public ApiFactory()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    public FakeTimeProvider Clock { get; } = new(Now);

    /// <summary>Anonymous requests per minute; high so tests don't trip the limiter unless they mean to.</summary>
    protected virtual int AnonymousPerMinute => 100_000;

    /// <summary>Whether to ingest fake data for the tracked universe before the first request.</summary>
    protected virtual bool Seed => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:AnalystsDb", _connectionString);
        builder.UseSetting("DataProvider:Name", "Fake");     // no API key, no network
        builder.UseSetting("Ingestion:Enabled", "false");    // tests trigger ingestion explicitly
        builder.UseSetting("ApiLimits:AnonymousPerMinute", AnonymousPerMinute.ToString());
        builder.UseSetting("ApiLimits:ApiKeyPerMinute", "100000");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.Configure<TrackingOptions>(o =>
            {
                o.Tickers = ["AAPL", "MSFT"];
                o.BenchmarkTicker = "SPY";
            });
        });
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        EnsureInitializedAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureInitializedAsync()
    {
        await _initGate.WaitAsync();
        try
        {
            if (_initialized) return;

            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AnalystsDbContext>().Database.MigrateAsync();
            if (Seed) await scope.ServiceProvider.GetRequiredService<IngestionService>().RunAsync(backfill: false, CancellationToken.None);
            _initialized = true;
        }
        finally
        {
            _initGate.Release();
        }
    }

    /// <summary>Creates an API key directly in the database.</summary>
    public async Task<string> CreateApiKeyAsync(ApiClientRole role, string? name = null)
    {
        await EnsureInitializedAsync();
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApiClientStore>()
            .CreateAsync(name ?? $"{role}-{Guid.NewGuid():N}", role, CancellationToken.None);
    }

    public HttpClient CreateClient(string apiKey)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeys.HeaderName, apiKey);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _keepAlive.Dispose();
    }
}

/// <summary><see cref="ApiFactory"/> with fake data for AAPL, MSFT and SPY ingested before the first request.</summary>
public sealed class SeededApiFactory : ApiFactory
{
    protected override bool Seed => true;
}

/// <summary><see cref="ApiFactory"/> with an anonymous limit of 3 requests per minute.</summary>
public sealed class RateLimitedApiFactory : ApiFactory
{
    protected override int AnonymousPerMinute => 3;
}
