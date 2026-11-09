using System.Net;
using System.Net.Http.Json;
using FinancialAnalystsAPI.Contracts;
using FinancialAnalystsAPI.Data;
using FinancialAnalystsAPI.Models;
using FinancialAnalystsAPI.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAnalystsAPI.Tests;

public class AnalystsEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public AnalystsEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAnalysts_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/analysts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAnalyst_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/analysts/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAnalyst_ReturnsFirmAndRatingCount()
    {
        int analystId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AnalystsDbContext>();
            var firm = new Firm { Name = "Endpoint Test Securities", NormalizedName = "endpoint test securities" };
            var analyst = new Analyst { Name = "Dana Example", NormalizedName = "dana example", Firm = firm };
            var stock = new Stock { Ticker = "ENDP", Name = "Endpoint Corp" };
            db.Ratings.Add(new Rating
            {
                Stock = stock, Firm = firm, Analyst = analyst, Date = new DateOnly(2026, 1, 5),
                Action = RatingAction.Initiate, RatingTo = "Buy", Category = RatingCategory.Buy,
                Source = "test", SourceKey = "ENDP:1",
            });
            await db.SaveChangesAsync();
            analystId = analyst.Id;
        }

        var dto = await _client.GetFromJsonAsync<AnalystDto>($"/api/analysts/{analystId}");

        Assert.NotNull(dto);
        Assert.Equal("Dana Example", dto.Name);
        Assert.Equal("Endpoint Test Securities", dto.FirmName);
        Assert.Equal(1, dto.RatingCount);
    }

    [Fact]
    public async Task WriteEndpoints_AreGone()
    {
        var response = await _client.PostAsJsonAsync("/api/analysts", new { Name = "X" });

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
