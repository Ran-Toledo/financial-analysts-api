using FinancialAnalystsAPI.Providers;
using FinancialAnalystsAPI.Providers.Fake;
using Microsoft.Extensions.Time.Testing;

namespace FinancialAnalystsAPI.Tests.Providers;

public class FakeAnalystDataProviderTests
{
    private static readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task IsDeterministic()
    {
        var a = await new FakeAnalystDataProvider(Clock).GetRatingsAsync("AAPL", new DateOnly(2024, 1, 1), default);
        var b = await new FakeAnalystDataProvider(Clock).GetRatingsAsync("AAPL", new DateOnly(2024, 1, 1), default);

        Assert.NotEmpty(a);
        Assert.Equal(a, b);
    }

    [Fact]
    public async Task Ratings_FallOnWeekdaysWithinRange_AndHaveUniqueKeys()
    {
        var since = new DateOnly(2024, 1, 1);
        var ratings = await new FakeAnalystDataProvider(Clock).GetRatingsAsync("MSFT", since, default);

        Assert.All(ratings, r =>
        {
            Assert.InRange(r.Date, since, new DateOnly(2026, 11, 9));
            Assert.DoesNotContain(r.Date.DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday });
        });
        Assert.Equal(ratings.Count, ratings.Select(r => r.SourceKey).Distinct().Count());
    }

    [Fact]
    public async Task Prices_AreWeekdaysOnly_AndStopAtToday()
    {
        var prices = await new FakeAnalystDataProvider(Clock)
            .GetDailyPricesAsync("AAPL", new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 31), default);

        Assert.Equal(new DateOnly(2026, 11, 9), prices[^1].Date);
        Assert.All(prices, p => Assert.DoesNotContain(p.Date.DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }));
    }

    [Fact]
    public async Task Benchmark_HasPricesButNoRatings()
    {
        var provider = new FakeAnalystDataProvider(Clock);

        Assert.Empty(await provider.GetRatingsAsync("SPY", new DateOnly(2020, 1, 1), default));
        Assert.NotEmpty(await provider.GetDailyPricesAsync("SPY", new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), default));
    }
}

public class DailyCallBudgetTests
{
    [Fact]
    public void AllowsUpToLimit_ThenRefuses_ThenResetsNextUtcDay()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 9, 23, 0, 0, TimeSpan.Zero));
        var budget = new DailyCallBudget(2, clock);

        Assert.True(budget.TryConsume());
        Assert.True(budget.TryConsume());
        Assert.False(budget.TryConsume());

        clock.Advance(TimeSpan.FromHours(2)); // past UTC midnight

        Assert.Equal(0, budget.Used);
        Assert.True(budget.TryConsume());
    }

    [Fact]
    public async Task Handler_ThrowsQuotaExceeded_WithoutCallingInnerHandler()
    {
        var inner = new Infrastructure.StubHttpHandler().Json("x", "[]");
        var budget = new DailyCallBudget(1, TimeProvider.System);
        using var client = new HttpClient(new DailyCallBudgetHandler(budget) { InnerHandler = inner })
        {
            BaseAddress = new Uri("https://fmp.test/"),
        };

        await client.GetAsync("x");
        await Assert.ThrowsAsync<ProviderQuotaExceededException>(() => client.GetAsync("x"));

        Assert.Single(inner.Requests);
    }
}
