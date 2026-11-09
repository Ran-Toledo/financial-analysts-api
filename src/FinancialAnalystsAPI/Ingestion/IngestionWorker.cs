using Microsoft.Extensions.Options;

namespace FinancialAnalystsAPI.Ingestion;

/// <summary>Runs <see cref="IngestionService"/> once a day at <c>Ingestion:RunAtUtc</c>.</summary>
public sealed class IngestionWorker(
    IServiceScopeFactory scopes,
    IOptions<IngestionOptions> options,
    TimeProvider time,
    ILogger<IngestionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        if (!o.Enabled)
        {
            logger.LogInformation("Scheduled ingestion is disabled (Ingestion:Enabled = false)");
            return;
        }

        try
        {
            if (o.RunOnStartup) await RunOnceAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = DelayUntilNextRun(time.GetUtcNow(), o.RunAtUtc);
                logger.LogInformation("Next ingestion run at {NextRun:u}", time.GetUtcNow() + delay);
                await Task.Delay(delay, time, stoppingToken);
                await RunOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is shutting down.
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IngestionService>().RunAsync(backfill: false, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Keep the schedule alive; the next run retries.
            logger.LogError(ex, "Scheduled ingestion run failed");
        }
    }

    /// <summary>Time until the next <paramref name="runAtUtc"/>, today if it is still ahead, otherwise tomorrow.</summary>
    internal static TimeSpan DelayUntilNextRun(DateTimeOffset now, TimeOnly runAtUtc)
    {
        var utcNow = now.UtcDateTime;
        var next = utcNow.Date + runAtUtc.ToTimeSpan();
        if (next <= utcNow) next = next.AddDays(1);
        return next - utcNow;
    }
}
