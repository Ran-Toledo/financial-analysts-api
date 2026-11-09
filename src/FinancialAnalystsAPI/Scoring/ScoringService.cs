using FinancialAnalystsAPI.Data;
using FinancialAnalystsAPI.Ingestion;
using FinancialAnalystsAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinancialAnalystsAPI.Scoring;

/// <param name="HoldBandsPct">The Hold band used at each horizon, in percent.</param>
public sealed record ScoringSummary(int Calls, int Outcomes, int Pending, bool HasBenchmark, IReadOnlyDictionary<int, double> HoldBandsPct);

/// <summary>
/// Rebuilds <see cref="CallOutcome"/> rows from ratings and prices. Outcomes are derived data, so every run recomputes
/// all of them in one transaction: a few thousand rows, well under a second.
/// </summary>
public sealed class ScoringService(
    AnalystsDbContext db,
    IOptions<ScoringOptions> options,
    IOptions<TrackingOptions> tracking,
    TimeProvider time,
    ILogger<ScoringService> logger)
{
    public async Task<ScoringSummary> RecomputeAsync(CancellationToken ct)
    {
        var o = options.Value;

        var ratings = await db.Ratings.AsNoTracking()
            .Select(r => new RatingRow(r.Id, r.Source, r.StockId, r.FirmId, r.AnalystId, r.Date, r.Category, r.PriceTargetTo))
            .ToListAsync(ct);
        var calls = ScoringEngine.SelectCalls(ratings, o.ReaffirmAfterDays);

        var benchmarkTicker = tracking.Value.BenchmarkTicker.Trim().ToUpperInvariant();
        var benchmarkId = await db.Stocks.Where(s => s.Ticker == benchmarkTicker).Select(s => (int?)s.Id).SingleOrDefaultAsync(ct);

        var stockIds = calls.Select(c => c.StockId).Append(benchmarkId ?? -1).Distinct().ToArray();
        var prices = await db.PriceSnapshots.AsNoTracking()
            .Where(p => stockIds.Contains(p.StockId))
            .Select(p => new { p.StockId, p.Date, p.Close, p.AdjClose })
            .ToListAsync(ct);
        var series = prices.GroupBy(p => p.StockId)
            .ToDictionary(g => g.Key, g => new PriceSeries(g.Select(p => (p.Date, p.Close, p.AdjClose))));

        PriceSeries? benchmark = benchmarkId is { } id ? series.GetValueOrDefault(id) : null;
        if (benchmark is null)
            logger.LogWarning("No prices for benchmark {Ticker}; excess returns equal raw returns until it is ingested", benchmarkTicker);

        var results = new List<OutcomeResult>();
        foreach (var call in calls)
        {
            if (!series.TryGetValue(call.StockId, out var stock)) continue;
            foreach (var horizon in o.Horizons)
            {
                if (ScoringEngine.Evaluate(call, horizon, stock, benchmark, o.HoldBandPct ?? 0) is { } r) results.Add(r);
            }
        }

        // Hold bands per horizon: fixed when configured, otherwise the median gap to the benchmark at that horizon.
        var holdBands = new Dictionary<int, double>();
        foreach (var horizon in o.Horizons)
        {
            holdBands[horizon] = o.HoldBandPct ?? ScoringEngine.AdaptiveHoldBandPct(results.Where(r => r.HorizonMonths == horizon));
        }
        if (o.HoldBandPct is null)
        {
            results = results.Select(r => ScoringEngine.ApplyHoldBand(r, holdBands[r.HorizonMonths])).ToList();
        }

        var now = time.GetUtcNow().UtcDateTime;
        var outcomes = results.Select(r => new CallOutcome
        {
            RatingId = r.RatingId,
            HorizonMonths = r.HorizonMonths,
            EndDate = r.EndDate,
            Category = r.Category,
            StartPrice = r.StartPrice,
            EndPrice = r.EndPrice,
            Return = Math.Round(r.Return, 6),
            BenchmarkReturn = Math.Round(r.BenchmarkReturn, 6),
            ExcessReturn = Math.Round(r.ExcessReturn, 6),
            DirectionalExcess = r.DirectionalExcess is { } d ? Math.Round(d, 6) : null,
            Hit = r.Hit,
            Target = r.Target,
            TargetHit = r.TargetHit,
            TargetError = r.TargetError is { } e ? Math.Round(e, 6) : null,
            ComputedAtUtc = now,
        }).ToList();

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await db.CallOutcomes.ExecuteDeleteAsync(ct);
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            db.CallOutcomes.AddRange(outcomes);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.AutoDetectChangesEnabled = true;
            await tx.CommitAsync(ct);
        }
        db.ChangeTracker.Clear();

        var summary = new ScoringSummary(calls.Count, outcomes.Count, calls.Count * o.Horizons.Length - outcomes.Count,
            benchmark is not null, holdBands.ToDictionary(b => b.Key, b => Math.Round(b.Value, 2)));
        logger.LogInformation(
            "Scoring: {Calls} calls, {Outcomes} matured outcomes, {Pending} call-horizons still pending; Hold bands {Bands}",
            summary.Calls, summary.Outcomes, summary.Pending,
            string.Join(", ", summary.HoldBandsPct.Select(b => $"{b.Key}m ±{b.Value}%")));
        return summary;
    }
}
