using System.Net;
using FinancialAnalystsAPI.Api;
using FinancialAnalystsAPI.Consensus;
using FinancialAnalystsAPI.Contracts;
using FinancialAnalystsAPI.Models;
using FinancialAnalystsAPI.Tests.Infrastructure;

namespace FinancialAnalystsAPI.Tests.Api;

/// <summary>Read endpoints over fake data for AAPL, MSFT and SPY (see <see cref="SeededApiFactory"/>).</summary>
public class ReadEndpointsTests(SeededApiFactory factory) : IClassFixture<SeededApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Stocks_ListsTrackedUniverseWithPaging()
    {
        var page1 = await _client.GetOkAsync<PagedResult<StockDto>>("/api/stocks?pageSize=2");
        var page2 = await _client.GetOkAsync<PagedResult<StockDto>>("/api/stocks?pageSize=2&page=2");

        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(["AAPL", "MSFT", "SPY"], page1.Items.Concat(page2.Items).Select(s => s.Ticker));
        Assert.All(page1.Items, s => Assert.NotNull(s.LastClose));
    }

    [Fact]
    public async Task Stocks_FilterBySectorAndSearch()
    {
        var tech = await _client.GetOkAsync<PagedResult<StockDto>>("/api/stocks?sector=TECHNOLOGY");
        var search = await _client.GetOkAsync<PagedResult<StockDto>>("/api/stocks?search=micro");

        Assert.Equal(["AAPL", "MSFT"], tech.Items.Select(s => s.Ticker));
        Assert.Equal("MSFT", Assert.Single(search.Items).Ticker);
    }

    [Fact]
    public async Task Stocks_SortByRatingCount()
    {
        var sorted = await _client.GetOkAsync<PagedResult<StockDto>>("/api/stocks?sort=ratingCount&dir=desc");

        Assert.Equal(sorted.Items.OrderByDescending(s => s.RatingCount).Select(s => s.Id), sorted.Items.Select(s => s.Id));
        Assert.Equal(0, sorted.Items[^1].RatingCount); // SPY has no ratings
    }

    [Fact]
    public async Task Stock_LookupIsCaseInsensitive_AndUnknownIs404()
    {
        var stock = await _client.GetOkAsync<StockDto>("/api/stocks/aapl");
        var missing = await _client.GetAsync("/api/stocks/ZZZZ");

        Assert.Equal("AAPL", stock.Ticker);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("No stock with ticker 'ZZZZ'.", (await missing.ProblemAsync()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Sectors_AreDistinctAndSorted()
    {
        var sectors = await _client.GetOkAsync<string[]>("/api/stocks/sectors");

        Assert.Equal(["Financial Services", "Technology"], sectors);
    }

    [Fact]
    public async Task StockRatings_FilterByActionAndDate()
    {
        var upgrades = await _client.GetOkAsync<PagedResult<RatingDto>>("/api/stocks/AAPL/ratings?action=Upgrade&pageSize=100");
        var recent = await _client.GetOkAsync<PagedResult<RatingDto>>("/api/stocks/AAPL/ratings?from=2026-01-01&to=2026-06-30&pageSize=100");

        Assert.NotEmpty(upgrades.Items);
        Assert.All(upgrades.Items, r => Assert.Equal(RatingAction.Upgrade, r.Action));
        Assert.All(recent.Items, r => Assert.InRange(r.Date, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)));
        Assert.Equal(recent.Items.OrderByDescending(r => r.Date).Select(r => r.Id), recent.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task Prices_DefaultToLastYear_Ascending()
    {
        var prices = await _client.GetOkAsync<PriceDto[]>("/api/stocks/AAPL/prices");
        var week = await _client.GetOkAsync<PriceDto[]>("/api/stocks/AAPL/prices?from=2026-11-02&to=2026-11-06");

        Assert.InRange(prices.Length, 240, 270);
        Assert.Equal(prices.OrderBy(p => p.Date).Select(p => p.Date), prices.Select(p => p.Date));
        Assert.Equal(5, week.Length);
    }

    [Fact]
    public async Task Consensus_SplitAddsUp_AndIncludesTargets()
    {
        var c = await _client.GetOkAsync<ConsensusDto>("/api/stocks/AAPL/consensus?lookbackDays=365");

        Assert.Equal(new DateOnly(2026, 11, 9), c.AsOf);
        Assert.True(c.Contributors > 0);
        Assert.Equal(c.Contributors, c.Buy + c.Hold + c.Sell);
        Assert.NotNull(c.Targets); // the fake provider supplies targets
        Assert.InRange(c.Targets.Mean, c.Targets.Low, c.Targets.High);
        Assert.NotNull(c.LastClose);
    }

    [Fact]
    public async Task ConsensusHistory_ReturnsOnePointPerMonth()
    {
        var history = await _client.GetOkAsync<ConsensusHistoryDto>("/api/stocks/AAPL/consensus/history?months=3");

        Assert.Equal([new(2026, 9, 30), new(2026, 10, 31), new DateOnly(2026, 11, 9)], history.Points.Select(p => p.AsOf));
        Assert.All(history.Points, p => Assert.NotNull(p.Close));
    }

    [Fact]
    public async Task Firms_And_Analysts_ListWithCounts()
    {
        var firms = await _client.GetOkAsync<PagedResult<FirmDto>>("/api/firms?sort=ratingCount&dir=desc");
        var analysts = await _client.GetOkAsync<PagedResult<AnalystDto>>("/api/analysts");
        var top = firms.Items[0];
        var firmRatings = await _client.GetOkAsync<PagedResult<RatingDto>>($"/api/firms/{top.Id}/ratings?pageSize=1");
        var analystRatings = await _client.GetOkAsync<PagedResult<RatingDto>>($"/api/analysts/{analysts.Items[0].Id}/ratings");

        Assert.Equal(firms.Items.OrderByDescending(f => f.RatingCount).Select(f => f.Id), firms.Items.Select(f => f.Id));
        Assert.Equal(top.RatingCount, firmRatings.TotalCount);
        Assert.InRange(top.StocksCovered, 1, 2);
        Assert.NotEmpty(analysts.Items);
        Assert.All(analystRatings.Items, r => Assert.Equal(analysts.Items[0].Id, r.AnalystId));
    }

    [Fact]
    public async Task Feed_DefaultsToRatingChanges()
    {
        var changes = await _client.GetOkAsync<PagedResult<RatingDto>>("/api/feed?pageSize=100");
        var all = await _client.GetOkAsync<PagedResult<RatingDto>>("/api/feed?includeAll=true&pageSize=1");
        var msft = await _client.GetOkAsync<PagedResult<RatingDto>>("/api/feed?ticker=msft&includeAll=true&pageSize=100");

        Assert.All(changes.Items, r => Assert.Contains(r.Action, RatingQueries.RatingChanges));
        Assert.True(all.TotalCount > changes.TotalCount);
        Assert.All(msft.Items, r => Assert.Equal("MSFT", r.Ticker));
    }

    [Theory]
    [InlineData("/api/stocks?sort=price")]
    [InlineData("/api/stocks?pageSize=101")]
    [InlineData("/api/stocks?page=0")]
    [InlineData("/api/stocks?dir=sideways")]
    [InlineData("/api/stocks/AAPL%3BDROP")]
    [InlineData("/api/feed?from=2026-02-01&to=2026-01-01")]
    [InlineData("/api/feed?action=Sideways")]
    [InlineData("/api/stocks/AAPL/prices?from=2000-01-01&to=2026-01-01")]
    [InlineData("/api/stocks/AAPL/consensus?lookbackDays=0")]
    public async Task InvalidInput_Returns400ProblemDetails(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
