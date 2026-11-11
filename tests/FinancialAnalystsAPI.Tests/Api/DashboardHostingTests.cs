using System.Net;
using FinancialAnalystsAPI.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace FinancialAnalystsAPI.Tests.Api;

/// <summary>The API serves the dashboard build from wwwroot, and its Swagger document feeds the dashboard's types.</summary>
public sealed class DashboardHostingTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private readonly string _webRoot = Directory.CreateTempSubdirectory("dashboard-").FullName;

    private HttpClient ClientWithDashboard()
    {
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><div id=root></div>");
        return factory.WithWebHostBuilder(b => b.UseSetting(WebHostDefaults.WebRootKey, _webRoot)).CreateClient();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/analysts/5")]
    [InlineData("/stocks/AAPL")]
    public async Task ClientRoutes_ServeIndexHtml(string path)
    {
        var response = await ClientWithDashboard().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/api/nope")]
    [InlineData("/api")]
    [InlineData("/assets/missing.js")]
    public async Task UnknownApiPathsAndFiles_Return404(string path)
    {
        var response = await ClientWithDashboard().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public void Swagger_MarksNonNullableFieldsRequired()
    {
        var doc = factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        var entry = doc.Components.Schemas["LeaderboardEntryDto"];
        var horizon = doc.Components.Schemas["HorizonScoreDto"];

        Assert.Superset(new HashSet<string> { "rank", "id", "name", "stats" }, entry.Required);
        Assert.DoesNotContain("trend", entry.Required);
        Assert.False(entry.Properties["name"].Nullable);
        Assert.True(horizon.Properties["stats"].Nullable);   // ScoreStats? stays nullable as a $ref
    }

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);
}
