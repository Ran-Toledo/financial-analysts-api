using System.Net;
using FinancialAnalystsAPI.Api;
using FinancialAnalystsAPI.Scoring;
using FinancialAnalystsAPI.Tests.Infrastructure;

namespace FinancialAnalystsAPI.Tests.Api;

/// <summary>Leaderboards over fake data (six fictional firms rating AAPL and MSFT for three years).</summary>
public class LeaderboardEndpointsTests(SeededApiFactory factory) : IClassFixture<SeededApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task FirmLeaderboard_RanksByScoreWithTrends()
    {
        var board = await _client.GetOkAsync<LeaderboardDto>("/api/leaderboards/firms?minCalls=1&trendMonths=6");

        Assert.Equal(12, board.HorizonMonths);
        Assert.NotEmpty(board.Items);
        Assert.Equal(Enumerable.Range(1, board.Items.Count), board.Items.Select(e => e.Rank));
        Assert.Equal(board.Items.OrderByDescending(e => e.Stats.Score).Select(e => e.Stats.Score), board.Items.Select(e => e.Stats.Score));
        Assert.All(board.Items, e =>
        {
            Assert.InRange(e.Stats.Score, 0, 100);
            Assert.InRange(e.Stats.HitRate, 0, 1);
            Assert.NotNull(e.Stats.TargetHitRate); // the fake provider supplies targets
            Assert.NotEmpty(e.Trend!);
        });
    }

    [Fact]
    public async Task MinCalls_FiltersAndSortChangesOrder()
    {
        var all = await _client.GetOkAsync<LeaderboardDto>("/api/leaderboards/firms?minCalls=1&trendMonths=0");
        var strict = await _client.GetOkAsync<LeaderboardDto>("/api/leaderboards/firms?minCalls=10000");
        var byName = await _client.GetOkAsync<LeaderboardDto>("/api/leaderboards/firms?minCalls=1&sort=name&trendMonths=0");

        Assert.Null(all.Items[0].Trend);
        Assert.Equal(0, strict.TotalCount);
        Assert.Equal(byName.Items.Select(e => e.Name).Order(StringComparer.Ordinal), byName.Items.Select(e => e.Name));
    }

    [Fact]
    public async Task AnalystLeaderboard_NamesFirms()
    {
        var board = await _client.GetOkAsync<LeaderboardDto>("/api/leaderboards/analysts?minCalls=1&horizon=3");

        Assert.NotEmpty(board.Items);
        Assert.All(board.Items, e => Assert.NotNull(e.FirmName));
    }

    [Fact]
    public async Task ScoreCard_CoversEveryHorizon()
    {
        var top = (await _client.GetOkAsync<LeaderboardDto>("/api/leaderboards/firms?minCalls=1&trendMonths=0")).Items[0];

        var card = await _client.GetOkAsync<ScoreCardDto>($"/api/firms/{top.Id}/scores?trendMonths=6");

        Assert.Equal(top.Name, card.Name);
        Assert.Equal([3, 6, 12], card.Horizons.Select(h => h.HorizonMonths));
        Assert.Equal(top.Stats, card.Horizons.Single(h => h.HorizonMonths == 12).Stats);
        Assert.NotEmpty(card.Trend);
    }

    [Fact]
    public async Task Outcomes_ListMaturedCalls()
    {
        var top = (await _client.GetOkAsync<LeaderboardDto>("/api/leaderboards/firms?minCalls=1&trendMonths=0")).Items[0];

        var outcomes = await _client.GetOkAsync<PagedResult<CallOutcomeDto>>($"/api/firms/{top.Id}/outcomes?pageSize=100");
        var misses = await _client.GetOkAsync<PagedResult<CallOutcomeDto>>($"/api/firms/{top.Id}/outcomes?hit=false&pageSize=100");

        Assert.Equal(top.Stats.Calls, outcomes.TotalCount);
        Assert.All(outcomes.Items, o =>
        {
            Assert.Equal(o.CallDate.AddMonths(12), o.EndDate);
            Assert.True(o.EndDate <= new DateOnly(2026, 11, 9));
        });
        Assert.All(misses.Items, o => Assert.False(o.Hit));
    }

    [Theory]
    [InlineData("/api/leaderboards/firms?horizon=9")]
    [InlineData("/api/leaderboards/firms?sort=luck")]
    [InlineData("/api/leaderboards/firms?minCalls=0")]
    [InlineData("/api/firms/1/outcomes?horizon=7")]
    [InlineData("/api/firms/1/scores?trendHorizon=1")]
    public async Task InvalidInput_Returns400(string url) =>
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync(url)).StatusCode);

    [Fact]
    public async Task UnknownSubject_Returns404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/firms/99999/scores")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/analysts/99999/outcomes")).StatusCode);
    }
}
