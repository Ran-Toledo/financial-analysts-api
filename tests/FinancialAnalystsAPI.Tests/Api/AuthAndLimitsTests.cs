using System.Net;
using FinancialAnalystsAPI.Api;
using FinancialAnalystsAPI.Auth;
using FinancialAnalystsAPI.Contracts;
using FinancialAnalystsAPI.Data;
using FinancialAnalystsAPI.Ingestion;
using FinancialAnalystsAPI.Models;
using FinancialAnalystsAPI.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAnalystsAPI.Tests.Api;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task AdminEndpoints_NeedAnAdminKey()
    {
        var reader = await factory.CreateApiKeyAsync(ApiClientRole.Reader);
        var admin = await factory.CreateApiKeyAsync(ApiClientRole.Admin);

        var anonymous = await factory.CreateClient().GetAsync("/api/admin/ingestion/runs");
        var asReader = await factory.CreateClient(reader).GetAsync("/api/admin/ingestion/runs");
        var asAdmin = await factory.CreateClient(admin).GetAsync("/api/admin/ingestion/runs");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal("ApiKey header=\"X-Api-Key\"", anonymous.Headers.WwwAuthenticate.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, asReader.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asAdmin.StatusCode);
    }

    [Fact]
    public async Task ReadEndpoints_AllowAnonymous_ButRejectBadKeys()
    {
        var anonymous = await factory.CreateClient().GetAsync("/api/stocks");
        var wrongKey = await factory.CreateClient("fa_not-a-real-key").GetAsync("/api/stocks");

        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongKey.StatusCode);
        Assert.Contains("doesn't match an active API key", (await wrongKey.ProblemAsync()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task RevokedKeys_AreRejected()
    {
        var name = $"revoked-{Guid.NewGuid():N}";
        var key = await factory.CreateApiKeyAsync(ApiClientRole.Admin, name);
        using (var scope = factory.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<ApiClientStore>().RevokeAsync(name, CancellationToken.None));

        var response = await factory.CreateClient(key).GetAsync("/api/admin/ingestion/runs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OnlyTheKeyHashIsStored()
    {
        var key = await factory.CreateApiKeyAsync(ApiClientRole.Reader, $"hash-{Guid.NewGuid():N}");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalystsDbContext>();
        var stored = db.ApiClients.Single(c => c.KeyHash == ApiKeys.Hash(key));

        Assert.StartsWith("fa_", key);
        Assert.Equal(64, stored.KeyHash.Length);
        Assert.Equal(key[..10], stored.KeyPrefix);
        Assert.DoesNotContain(db.ApiClients, c => c.KeyHash == key);
    }

    [Fact]
    public async Task AdminCanTriggerIngestion_AndFollowTheRun()
    {
        var admin = factory.CreateClient(await factory.CreateApiKeyAsync(ApiClientRole.Admin));

        var start = await admin.PostAsync("/api/admin/ingestion/run", null);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        Assert.Equal("/api/admin/ingestion/runs", start.Headers.Location?.OriginalString);

        IngestionRunDto? run = null;
        for (var i = 0; i < 100 && run?.Status is null or IngestionStatus.Running; i++)
        {
            await Task.Delay(100);
            run = (await admin.GetOkAsync<PagedResult<IngestionRunDto>>("/api/admin/ingestion/runs?pageSize=1")).Items.FirstOrDefault();
        }

        Assert.Equal(IngestionStatus.Succeeded, run?.Status);
        Assert.Equal(3, run!.StocksProcessed);
    }

    [Fact]
    public async Task SecondTrigger_WhileRunning_Returns409()
    {
        var coordinator = factory.Services.GetRequiredService<IngestionCoordinator>();
        var admin = factory.CreateClient(await factory.CreateApiKeyAsync(ApiClientRole.Admin));

        Assert.True(coordinator.TryStartInBackground(backfill: false));
        var second = await admin.PostAsync("/api/admin/ingestion/run", null);
        await coordinator.RunAsync(backfill: false, CancellationToken.None); // waits for the first run to finish

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
}

public class RateLimitTests(RateLimitedApiFactory factory) : IClassFixture<RateLimitedApiFactory>
{
    [Fact]
    public async Task AnonymousCallers_GetA429WithRetryAfter_KeyedCallersDont()
    {
        var anonymous = factory.CreateClient();
        var keyed = factory.CreateClient(await factory.CreateApiKeyAsync(ApiClientRole.Reader));

        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage last = null!;
        for (var i = 0; i < 4; i++)
        {
            last = await anonymous.GetAsync("/api/stocks");
            statuses.Add(last.StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);
        Assert.NotNull(last.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.OK, (await keyed.GetAsync("/api/stocks")).StatusCode);
    }

    [Fact]
    public async Task NonApiPaths_AreNotLimited()
    {
        var client = factory.CreateClient();
        for (var i = 0; i < 5; i++)
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.GetAsync("/not-an-api-path")).StatusCode);
    }
}
