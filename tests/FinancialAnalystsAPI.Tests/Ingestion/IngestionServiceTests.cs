using FinancialAnalystsAPI.Ingestion;
using FinancialAnalystsAPI.Models;
using FinancialAnalystsAPI.Providers;
using FinancialAnalystsAPI.Providers.Fake;
using FinancialAnalystsAPI.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace FinancialAnalystsAPI.Tests.Ingestion;

public sealed class IngestionServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 9);
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 9, 23, 0, 0, TimeSpan.Zero));
    private readonly TestDb _testDb = new();

    public void Dispose() => _testDb.Dispose();

    private Task<IngestionRun> RunAsync(IAnalystDataProvider provider, string[] tickers, bool backfill = false, int years = 2)
    {
        var db = _testDb.CreateContext();
        var service = new IngestionService(db, provider,
            Options.Create(new TrackingOptions { Tickers = tickers, BenchmarkTicker = "SPY" }),
            Options.Create(new IngestionOptions { BackfillYears = years }),
            _clock, NullLogger<IngestionService>.Instance);
        return service.RunAsync(backfill, CancellationToken.None);
    }

    [Fact]
    public async Task FirstRun_LoadsHistory_SecondRunAddsNothing()
    {
        var provider = new FakeAnalystDataProvider(_clock);

        var first = await RunAsync(provider, ["AAPL", "MSFT"]);
        var second = await RunAsync(provider, ["AAPL", "MSFT"]);
        var backfill = await RunAsync(provider, ["AAPL", "MSFT"], backfill: true);

        Assert.Equal(IngestionStatus.Succeeded, first.Status);
        Assert.Equal(3, first.StocksProcessed); // AAPL, MSFT and the SPY benchmark
        Assert.True(first.PricesAdded > 1000);
        Assert.True(first.RatingsAdded > 20);

        Assert.Equal((0, 0, 0), (second.PricesAdded, second.RatingsAdded, second.RatingsUpdated));
        Assert.Equal((0, 0, 0), (backfill.PricesAdded, backfill.RatingsAdded, backfill.RatingsUpdated));

        await using var db = _testDb.CreateContext();
        Assert.Equal(first.RatingsAdded, await db.Ratings.CountAsync());
        Assert.Equal(3, await db.IngestionRuns.CountAsync(r => r.Status == IngestionStatus.Succeeded));
        Assert.True((await db.Stocks.SingleAsync(s => s.Ticker == "SPY")).IsBenchmark);
    }

    [Fact]
    public async Task DerivedFields_ChainTargetsPerAnalyst_AndFillPriceAtCall()
    {
        await RunAsync(new FakeAnalystDataProvider(_clock), ["NVDA"]);

        await using var db = _testDb.CreateContext();
        var ratings = await db.Ratings.Include(r => r.Analyst).OrderBy(r => r.Date).ToListAsync();

        Assert.All(ratings, r => Assert.NotNull(r.PriceAtCall));
        foreach (var chain in ratings.GroupBy(r => r.AnalystId))
        {
            var calls = chain.ToList();
            Assert.Null(calls[0].PriceTargetFrom);
            for (var i = 1; i < calls.Count; i++)
                Assert.Equal(calls[i - 1].PriceTargetTo, calls[i].PriceTargetFrom);
        }

        Assert.Contains(ratings, r => r.RatingCarriedForward && r.RatingTo is not null);
        Assert.Contains(ratings, r => r.Action is RatingAction.TargetRaise or RatingAction.TargetLower);
    }

    [Fact]
    public async Task LateGrade_IsMergedIntoTheExistingTargetOnlyRating()
    {
        var day = Today.AddDays(-3);
        var provider = new ScriptedProvider
        {
            Prices = (_, from, to) => [new ProviderPrice(day, 50, 50)],
            Ratings = (_, _) =>
            [
                new(day.AddDays(-30), "Acme Securities", "Pat Doe", null, "Hold", "init", 55, null, "K:old"),
                new(day, "Acme Securities", "Pat Doe", null, null, null, 60, 49.5, "K:new"),
            ],
        };

        await RunAsync(provider, ["ACME"]);

        await using (var db = _testDb.CreateContext())
        {
            var before = await db.Ratings.SingleAsync(r => r.SourceKey == "K:new");
            Assert.True(before.RatingCarriedForward);
            Assert.Equal("Hold", before.RatingTo);
            Assert.Equal(RatingAction.TargetRaise, before.Action);
            Assert.Equal(55, before.PriceTargetFrom);
            Assert.Equal(50, before.PriceAtCall); // stored close wins over the provider's price
        }

        // Next day the provider also reports the upgrade behind that target.
        provider.Ratings = (_, _) => [new(day, "ACME SECURITIES INC", null, "Hold", "Buy", "upgrade", null, null, "K:new")];
        var run = await RunAsync(provider, ["ACME"]);

        Assert.Equal((0, 1), (run.RatingsAdded, run.RatingsUpdated));
        await using (var db = _testDb.CreateContext())
        {
            var after = await db.Ratings.Include(r => r.Analyst).SingleAsync(r => r.SourceKey == "K:new");
            Assert.False(after.RatingCarriedForward);
            Assert.Equal(("Hold", "Buy", RatingCategory.Buy, RatingAction.Upgrade), (after.RatingFrom, after.RatingTo, after.Category, after.Action));
            Assert.Equal(60, after.PriceTargetTo);   // kept: the second fetch had no target
            Assert.Equal("Pat Doe", after.Analyst?.Name);
            Assert.Equal(1, await db.Firms.CountAsync()); // "ACME SECURITIES INC" matched the existing firm
        }
    }

    [Fact]
    public async Task OneFailingTicker_DoesNotStopTheOthers()
    {
        var provider = new ScriptedProvider
        {
            Prices = (t, _, _) => t == "BAD" ? throw new HttpRequestException("boom") : [new ProviderPrice(Today, 10, 10)],
        };

        var run = await RunAsync(provider, ["GOOD", "BAD"]);

        Assert.Equal(IngestionStatus.PartiallySucceeded, run.Status);
        Assert.Equal(2, run.StocksProcessed); // GOOD and SPY
        Assert.Contains("BAD: boom", run.Error);
    }

    [Fact]
    public async Task QuotaExceeded_StopsTheRun()
    {
        var calls = 0;
        var provider = new ScriptedProvider
        {
            Prices = (_, _, _) => ++calls > 1 ? throw new ProviderQuotaExceededException("budget used up") : [],
        };

        var run = await RunAsync(provider, ["A", "B", "C"]);

        Assert.Equal(IngestionStatus.PartiallySucceeded, run.Status);
        Assert.Equal(1, run.StocksProcessed);
        Assert.Equal(2, calls); // C and SPY never attempted
    }

    [Fact]
    public async Task TickersRemovedFromConfig_AreUntracked()
    {
        var provider = new ScriptedProvider();
        await RunAsync(provider, ["KEEP", "DROP"]);

        await RunAsync(provider, ["KEEP"]);

        await using var db = _testDb.CreateContext();
        var tracked = await db.Stocks.Where(s => s.IsTracked).Select(s => s.Ticker).OrderBy(t => t).ToListAsync();
        Assert.Equal(["KEEP", "SPY"], tracked);
    }

    [Fact]
    public async Task RunRecordsOutcome()
    {
        var run = await RunAsync(new ScriptedProvider(), ["X"]);

        await using var db = _testDb.CreateContext();
        var stored = await db.IngestionRuns.SingleAsync();
        Assert.Equal(run.Id, stored.Id);
        Assert.Equal(IngestionStatus.Succeeded, stored.Status);
        Assert.Equal("scripted", stored.Provider);
        Assert.NotNull(stored.FinishedAtUtc);
    }
}

public class IngestionWorkerScheduleTests
{
    [Theory]
    [InlineData("2026-11-09T10:00:00Z", "22:30", 12.5)]   // later today
    [InlineData("2026-11-09T22:30:00Z", "22:30", 24)]     // exactly now → tomorrow
    [InlineData("2026-11-09T23:00:00Z", "22:30", 23.5)]   // already passed → tomorrow
    public void DelayUntilNextRun(string now, string runAt, double expectedHours)
    {
        var delay = IngestionWorker.DelayUntilNextRun(DateTimeOffset.Parse(now), TimeOnly.Parse(runAt));

        Assert.Equal(TimeSpan.FromHours(expectedHours), delay);
    }
}
