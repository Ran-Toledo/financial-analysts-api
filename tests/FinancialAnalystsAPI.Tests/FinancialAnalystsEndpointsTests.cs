using System.Net;
using System.Net.Http.Json;
using FinancialAnalystsAPI.Models;
using FinancialAnalystsAPI.Tests.Infrastructure;

namespace FinancialAnalystsAPI.Tests;

public class FinancialAnalystsEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public FinancialAnalystsEndpointsTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAnalysts_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/FinancialAnalysts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAnalyst_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/FinancialAnalysts/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddAnalyst_ThenGetById_RoundTrips()
    {
        var created = await _client.PostAsJsonAsync("/api/FinancialAnalysts",
            new FinancialAnalyst { Name = "Jane Doe", Expertise = "Semiconductors", Rating = 4.5 });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<FinancialAnalyst>();
        Assert.NotNull(body);

        var fetched = await _client.GetFromJsonAsync<FinancialAnalyst>($"/api/FinancialAnalysts/{body.Id}");

        Assert.NotNull(fetched);
        Assert.Equal("Jane Doe", fetched.Name);
        Assert.Equal("Semiconductors", fetched.Expertise);
    }
}
