using System.Net;
using FinancialAnalystsAPI.Providers;
using FinancialAnalystsAPI.Providers.Fmp;
using FinancialAnalystsAPI.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FinancialAnalystsAPI.Tests.Providers;

// The JSON below mirrors FMP's documented "stable" response shapes. It is hand-written, not captured
// from the live API; replace it with saved real responses once an API key is available (plan step 1.1).
public class FmpAnalystDataProviderTests
{
    private const string ApiKey = "test-key-123";
    private readonly StubHttpHandler _http = new();

    private FmpAnalystDataProvider CreateProvider(int pageSize = 100, int maxPages = 10) =>
        new(new HttpClient(_http) { BaseAddress = new Uri("https://fmp.test/stable/") },
            Options.Create(new FmpOptions { ApiKey = ApiKey, PriceTargetPageSize = pageSize, MaxPriceTargetPages = maxPages }),
            NullLogger<FmpAnalystDataProvider>.Instance);

    [Fact]
    public async Task GetRatings_MergesGradeAndTargetFromSameFirmAndDay()
    {
        _http.Json("stable/grades", """
            [
              { "symbol": "AAPL", "date": "2026-10-02", "gradingCompany": "Morgan Stanley", "previousGrade": "Equal-Weight", "newGrade": "Overweight", "action": "upgrade" },
              { "symbol": "AAPL", "date": "2026-09-15", "gradingCompany": "Barclays", "previousGrade": "Underweight", "newGrade": "Underweight", "action": "maintain" }
            ]
            """);
        _http.Json("stable/price-target-news", """
            [
              { "symbol": "AAPL", "publishedDate": "2026-10-02T13:05:00.000Z", "analystName": "Erik Example", "analystCompany": "Morgan Stanley & Co.", "priceTarget": 260, "adjPriceTarget": 260, "priceWhenPosted": 231.5 },
              { "symbol": "AAPL", "publishedDate": "2026-09-20T09:00:00.000Z", "analystName": "", "analystCompany": "Wedbush", "priceTarget": 300, "adjPriceTarget": null, "priceWhenPosted": 225 }
            ]
            """);

        var ratings = await CreateProvider().GetRatingsAsync("AAPL", new DateOnly(2026, 9, 1), CancellationToken.None);

        Assert.Equal(3, ratings.Count);

        var ms = Assert.Single(ratings, r => r.Date == new DateOnly(2026, 10, 2));
        Assert.Equal("Morgan Stanley", ms.Firm);
        Assert.Equal("Erik Example", ms.Analyst);
        Assert.Equal(("Equal-Weight", "Overweight", "upgrade"), (ms.RatingFrom, ms.RatingTo, ms.Action));
        Assert.Equal(260, ms.PriceTarget);
        Assert.Equal(231.5, ms.PriceWhenPosted);
        Assert.Equal("AAPL:2026-10-02:morgan stanley", ms.SourceKey);

        var gradeOnly = Assert.Single(ratings, r => r.Firm == "Barclays");
        Assert.Null(gradeOnly.Analyst);
        Assert.Null(gradeOnly.PriceTarget);

        var targetOnly = Assert.Single(ratings, r => r.Firm == "Wedbush");
        Assert.Null(targetOnly.Analyst);   // blank analyst name
        Assert.Null(targetOnly.RatingTo);
        Assert.Equal(300, targetOnly.PriceTarget); // falls back to priceTarget when adjPriceTarget is null
    }

    [Fact]
    public async Task GetRatings_DropsItemsBeforeSince_AndStopsPagingPastIt()
    {
        _http.Json("stable/grades", "[]");
        _http.Json("stable/price-target-news", """
            [
              { "publishedDate": "2026-10-01T00:00:00Z", "analystCompany": "Firm A", "priceTarget": 10 },
              { "publishedDate": "2026-08-01T00:00:00Z", "analystCompany": "Firm B", "priceTarget": 11 }
            ]
            """);

        var ratings = await CreateProvider(pageSize: 2).GetRatingsAsync("AAPL", new DateOnly(2026, 9, 1), CancellationToken.None);

        Assert.Equal("Firm A", Assert.Single(ratings).Firm);
        Assert.Single(_http.Requests, u => u.AbsolutePath.EndsWith("price-target-news")); // page 0 reached past `since`
    }

    [Fact]
    public async Task GetRatings_RespectsPageCap()
    {
        _http.Json("stable/grades", "[]");
        _http.Json("stable/price-target-news", """[ { "publishedDate": "2026-10-01T00:00:00Z", "analystCompany": "Firm A", "priceTarget": 10 } ]""");

        await CreateProvider(pageSize: 1, maxPages: 3).GetRatingsAsync("AAPL", new DateOnly(2020, 1, 1), CancellationToken.None);

        Assert.Equal(3, _http.Requests.Count(u => u.AbsolutePath.EndsWith("price-target-news")));
    }

    [Fact]
    public async Task GetDailyPrices_JoinsAdjustedCloseByDate()
    {
        _http.Json("stable/historical-price-eod/full", """
            [ { "date": "2026-11-06", "close": 101.5 }, { "date": "2026-11-05", "close": 100.0 } ]
            """);
        _http.Json("stable/historical-price-eod/dividend-adjusted", """
            [ { "date": "2026-11-06", "adjClose": 101.2 } ]
            """);

        var prices = await CreateProvider().GetDailyPricesAsync("AAPL", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 8), CancellationToken.None);

        Assert.Collection(prices,
            p => Assert.Equal((new DateOnly(2026, 11, 5), 100.0, (double?)null), (p.Date, p.Close, p.AdjClose)),
            p => Assert.Equal((new DateOnly(2026, 11, 6), 101.5, (double?)101.2), (p.Date, p.Close, p.AdjClose)));
    }

    [Fact]
    public async Task GetStockProfile_MapsFields()
    {
        _http.Json("stable/profile", """
            [ { "symbol": "AAPL", "companyName": "Apple Inc.", "sector": "Technology", "industry": "Consumer Electronics", "exchange": "NASDAQ" } ]
            """);

        var profile = await CreateProvider().GetStockProfileAsync("AAPL", CancellationToken.None);

        Assert.Equal(new ProviderStockProfile("AAPL", "Apple Inc.", "Technology", "Consumer Electronics", "NASDAQ"), profile);
    }

    [Fact]
    public async Task Requests_CarrySymbolAndApiKey()
    {
        _http.Json("stable/profile", "[]");

        await CreateProvider().GetStockProfileAsync("MSFT", CancellationToken.None);

        var query = Assert.Single(_http.Requests).Query;
        Assert.Contains("symbol=MSFT", query);
        Assert.Contains($"apikey={ApiKey}", query);
    }

    [Fact]
    public async Task ErrorResponses_DoNotLeakTheApiKey()
    {
        _http.On("stable/profile", _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        _http.Json("stable/grades", """{ "Error Message": "Premium endpoint" }""");

        var status = await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateProvider().GetStockProfileAsync("AAPL", CancellationToken.None));
        var errorObject = await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateProvider().GetRatingsAsync("AAPL", new DateOnly(2026, 1, 1), CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);
        Assert.DoesNotContain(ApiKey, status.Message);
        Assert.Contains("Premium endpoint", errorObject.Message);
        Assert.DoesNotContain(ApiKey, errorObject.Message);
    }
}
