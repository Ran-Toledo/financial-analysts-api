using FinancialAnalystsAPI.Api;
using FinancialAnalystsAPI.Models;
using Microsoft.AspNetCore.OutputCaching;

namespace FinancialAnalystsAPI.Ingestion;

/// <summary>
/// Ensures one ingestion run at a time inside the web process (scheduled or admin-triggered), and clears
/// cached API responses when a run finishes. The <c>ingest</c> command runs in its own process and doesn't go
/// through here, so cached responses can lag it by up to <c>ApiLimits:CacheMinutes</c>.
/// </summary>
public sealed class IngestionCoordinator(
    IServiceScopeFactory scopes,
    IOutputCacheStore cache,
    IHostApplicationLifetime lifetime,
    ILogger<IngestionCoordinator> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsRunning => _gate.CurrentCount == 0;

    /// <summary>Waits for any run in progress, then runs.</summary>
    public async Task<IngestionRun> RunAsync(bool backfill, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await RunCoreAsync(backfill, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Starts a run in the background unless one is already in progress.</summary>
    public bool TryStartInBackground(bool backfill)
    {
        if (!_gate.Wait(0)) return false;

        var stopping = lifetime.ApplicationStopping;
        _ = Task.Run(async () =>
        {
            try
            {
                await RunCoreAsync(backfill, stopping);
            }
            catch (Exception ex) when (!stopping.IsCancellationRequested)
            {
                logger.LogError(ex, "Background ingestion run failed");
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
            finally
            {
                _gate.Release();
            }
        }, CancellationToken.None);

        return true;
    }

    private async Task<IngestionRun> RunCoreAsync(bool backfill, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var run = await scope.ServiceProvider.GetRequiredService<IngestionService>().RunAsync(backfill, ct);
        await cache.EvictByTagAsync(CachePolicies.MarketDataTag, CancellationToken.None);
        return run;
    }
}
