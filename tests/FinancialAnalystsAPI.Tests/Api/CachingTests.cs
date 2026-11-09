using FinancialAnalystsAPI.Contracts;
using FinancialAnalystsAPI.Data;
using FinancialAnalystsAPI.Ingestion;
using FinancialAnalystsAPI.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAnalystsAPI.Tests.Api;

/// <summary>Own fixture instance: this test edits the database underneath the cache.</summary>
public class CachingTests(SeededApiFactory factory) : IClassFixture<SeededApiFactory>
{
    [Fact]
    public async Task ReadsAreCached_UntilAnIngestionRunEvictsThem()
    {
        var client = factory.CreateClient();
        var before = await client.GetOkAsync<StockDto>("/api/stocks/MSFT");

        await RenameAsync("MSFT", "Renamed Corp");
        var cached = await client.GetOkAsync<StockDto>("/api/stocks/MSFT");

        await factory.Services.GetRequiredService<IngestionCoordinator>().RunAsync(backfill: false, CancellationToken.None);
        var fresh = await client.GetOkAsync<StockDto>("/api/stocks/MSFT");

        Assert.Equal(before.Name, cached.Name);   // served from cache despite the database change
        Assert.Equal("Renamed Corp", fresh.Name); // evicted by the run (which doesn't refetch existing profiles)
    }

    [Fact]
    public async Task CacheKey_IncludesTheQueryString()
    {
        var client = factory.CreateClient();

        var page1 = await client.GetOkAsync<PagedResultProbe>("/api/stocks?pageSize=1&page=1");
        var page2 = await client.GetOkAsync<PagedResultProbe>("/api/stocks?pageSize=1&page=2");

        Assert.NotEqual(page1.Items[0].Ticker, page2.Items[0].Ticker);
    }

    private sealed record PagedResultProbe(List<StockDto> Items);

    private async Task RenameAsync(string ticker, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalystsDbContext>();
        await db.Stocks.Where(s => s.Ticker == ticker).ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, name));
    }
}
