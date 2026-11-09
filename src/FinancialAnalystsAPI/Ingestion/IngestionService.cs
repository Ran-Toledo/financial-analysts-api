using FinancialAnalystsAPI.Data;
using FinancialAnalystsAPI.Models;
using FinancialAnalystsAPI.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinancialAnalystsAPI.Ingestion;

/// <summary>
/// Pulls profiles, daily prices and ratings for the tracked universe into the database.
/// Idempotent: prices are keyed by (stock, date) and ratings by (source, source key), so re-running
/// a day, or the whole history with <c>backfill</c>, adds nothing that is already stored.
/// </summary>
public sealed class IngestionService(
    AnalystsDbContext db,
    IAnalystDataProvider provider,
    IOptions<TrackingOptions> tracking,
    IOptions<IngestionOptions> options,
    TimeProvider time,
    ILogger<IngestionService> logger)
{
    private sealed record TickerResult(int PricesAdded, int RatingsAdded, int RatingsUpdated);

    /// <param name="backfill">
    /// Ignore stored watermarks and re-fetch <c>Ingestion:BackfillYears</c> of history for every ticker.
    /// A ticker's first ingestion always does this.
    /// </param>
    public async Task<IngestionRun> RunAsync(bool backfill, CancellationToken ct)
    {
        var tickers = tracking.Value.AllTickers().ToArray();
        var benchmark = tracking.Value.BenchmarkTicker.Trim().ToUpperInvariant();

        var run = new IngestionRun
        {
            Provider = provider.Name,
            Backfill = backfill,
            StartedAtUtc = UtcNow(),
            Status = IngestionStatus.Running,
        };
        db.IngestionRuns.Add(run);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Ingestion run {RunId} started: {Count} tickers from {Provider}{Mode}",
            run.Id, tickers.Length, provider.Name, backfill ? " (backfill)" : "");

        var failures = new List<string>();
        var cancelled = false;

        try
        {
            await db.Stocks
                .Where(s => (s.IsTracked || s.IsBenchmark) && !tickers.Contains(s.Ticker))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsTracked, false).SetProperty(x => x.IsBenchmark, false), ct);

            foreach (var ticker in tickers)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var result = await IngestTickerAsync(ticker, ticker == benchmark, backfill, run, ct);
                    run.StocksProcessed++;
                    run.RatingsAdded += result.RatingsAdded;
                    run.RatingsUpdated += result.RatingsUpdated;
                    logger.LogInformation("{Ticker}: +{Prices} prices, +{Added} ratings, {Updated} ratings updated",
                        ticker, result.PricesAdded, result.RatingsAdded, result.RatingsUpdated);
                }
                catch (Exception ex) when (ex is ProviderQuotaExceededException or ProviderAuthenticationException)
                {
                    // Run-wide failures: every remaining ticker would fail the same way.
                    failures.Add($"{ticker}: {ex.Message} Remaining tickers skipped.");
                    logger.LogWarning("Stopping run {RunId} at {Ticker}: {Message}", run.Id, ticker, ex.Message);
                    db.ChangeTracker.Clear();
                    break;
                }
                catch (Exception ex) when (!ct.IsCancellationRequested) // includes HTTP timeouts, which surface as TaskCanceledException
                {
                    // One bad ticker must not stop the others; discard its half-applied changes.
                    failures.Add($"{ticker}: {ex.Message}");
                    logger.LogError(ex, "Ingestion failed for {Ticker}", ticker);
                    db.ChangeTracker.Clear();
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            cancelled = true;
            failures.Add("Run cancelled.");
        }

        run.FinishedAtUtc = UtcNow();
        run.Status = failures.Count == 0 ? IngestionStatus.Succeeded
            : run.StocksProcessed > 0 ? IngestionStatus.PartiallySucceeded
            : IngestionStatus.Failed;
        run.Error = failures.Count == 0 ? null : Truncate(string.Join('\n', failures), 4000);

        db.ChangeTracker.Clear();
        db.IngestionRuns.Update(run);
        await db.SaveChangesAsync(CancellationToken.None); // record the outcome even when cancelled

        logger.LogInformation(
            "Ingestion run {RunId} {Status}: {Stocks}/{Total} stocks, +{Prices} prices, +{Added} ratings, {Updated} updated",
            run.Id, run.Status, run.StocksProcessed, tickers.Length, run.PricesAdded, run.RatingsAdded, run.RatingsUpdated);

        if (cancelled) ct.ThrowIfCancellationRequested();
        return run;
    }

    /// <remarks>
    /// Profile and prices are saved before ratings are fetched, so they persist (and count in <paramref name="run"/>)
    /// even if the ratings step fails.
    /// </remarks>
    private async Task<TickerResult> IngestTickerAsync(
        string ticker, bool isBenchmark, bool backfill, IngestionRun run, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(UtcNow());
        var historyStart = today.AddYears(-options.Value.BackfillYears);

        var stock = await db.Stocks.SingleOrDefaultAsync(s => s.Ticker == ticker, ct);
        if (stock is null || backfill || stock.Name == stock.Ticker)
        {
            var profile = await provider.GetStockProfileAsync(ticker, ct);
            if (stock is null)
            {
                stock = new Stock { Ticker = ticker };
                db.Stocks.Add(stock);
            }

            if (profile is not null)
            {
                stock.Name = profile.Name;
                stock.Sector = profile.Sector;
                stock.Industry = profile.Industry;
                stock.Exchange = profile.Exchange;
            }
            else if (string.IsNullOrEmpty(stock.Name))
            {
                stock.Name = ticker; // retried on the next run
                logger.LogWarning("No profile returned for {Ticker}", ticker);
            }
        }

        stock.IsTracked = true;
        stock.IsBenchmark = isBenchmark;
        await db.SaveChangesAsync(ct);

        var pricesAdded = await IngestPricesAsync(stock, backfill, historyStart, today, ct);
        run.PricesAdded += pricesAdded;

        var (ratingsAdded, ratingsUpdated) = await IngestRatingsAsync(stock, backfill, historyStart, today, ct);
        return new TickerResult(pricesAdded, ratingsAdded, ratingsUpdated);
    }

    private async Task<int> IngestPricesAsync(Stock stock, bool backfill, DateOnly historyStart, DateOnly today, CancellationToken ct)
    {
        var last = await db.PriceSnapshots
            .Where(p => p.StockId == stock.Id)
            .OrderByDescending(p => p.Date)
            .Select(p => (DateOnly?)p.Date)
            .FirstOrDefaultAsync(ct);

        var from = backfill || last is null ? historyStart : last.Value.AddDays(1);
        if (from > today) return 0;

        var prices = await provider.GetDailyPricesAsync(stock.Ticker, from, today, ct);

        // Backfill may revise stored closes (e.g. after a split re-adjusts history).
        var existing = await db.PriceSnapshots
            .Where(p => p.StockId == stock.Id && p.Date >= from)
            .ToDictionaryAsync(p => p.Date, ct);

        var added = 0;
        foreach (var p in prices.Where(p => p.Date >= from && p.Date <= today))
        {
            if (existing.TryGetValue(p.Date, out var snapshot))
            {
                snapshot.Close = p.Close;
                snapshot.AdjClose = p.AdjClose;
                continue;
            }

            snapshot = new PriceSnapshot { StockId = stock.Id, Date = p.Date, Close = p.Close, AdjClose = p.AdjClose };
            db.PriceSnapshots.Add(snapshot);
            existing[p.Date] = snapshot;
            added++;
        }

        await db.SaveChangesAsync(ct);
        return added;
    }

    private async Task<(int Added, int Updated)> IngestRatingsAsync(
        Stock stock, bool backfill, DateOnly historyStart, DateOnly today, CancellationToken ct)
    {
        // A stock has at most a few hundred ratings, so derived fields are recomputed over all of them.
        var all = await db.Ratings
            .Include(r => r.Firm)
            .Include(r => r.Analyst)
            .Where(r => r.StockId == stock.Id)
            .ToListAsync(ct);

        var fromThisProvider = all.Where(r => r.Source == provider.Name).ToList();
        var bySourceKey = fromThisProvider.ToDictionary(r => r.SourceKey);
        var lastDate = fromThisProvider.Count == 0 ? (DateOnly?)null : fromThisProvider.Max(r => r.Date);
        var since = backfill || lastDate is null ? historyStart : lastDate.Value.AddDays(-options.Value.RatingsOverlapDays);

        var incoming = await provider.GetRatingsAsync(stock.Ticker, since, ct);

        var firms = await db.Firms.ToDictionaryAsync(f => f.NormalizedName, ct);
        var analysts = await db.Analysts.Include(a => a.Firm)
            .ToDictionaryAsync(a => (Firm: a.Firm.NormalizedName, Analyst: a.NormalizedName), ct);

        var now = UtcNow();
        var added = 0;

        foreach (var r in incoming.Where(r => r.Date >= since && r.Date <= today))
        {
            var firmKey = NameNormalizer.Normalize(r.Firm);
            if (firmKey.Length == 0) continue;

            if (!firms.TryGetValue(firmKey, out var firm))
            {
                firm = new Firm { Name = r.Firm.Trim(), NormalizedName = firmKey };
                db.Firms.Add(firm);
                firms[firmKey] = firm;
            }

            Analyst? analyst = null;
            var analystKey = NameNormalizer.Normalize(r.Analyst);
            if (analystKey.Length > 0 && !analysts.TryGetValue((firmKey, analystKey), out analyst))
            {
                analyst = new Analyst { Name = r.Analyst!.Trim(), NormalizedName = analystKey, Firm = firm };
                db.Analysts.Add(analyst);
                analysts[(firmKey, analystKey)] = analyst;
            }

            if (!bySourceKey.TryGetValue(r.SourceKey, out var rating))
            {
                rating = new Rating
                {
                    Stock = stock,
                    Firm = firm,
                    Date = r.Date,
                    Source = provider.Name,
                    SourceKey = r.SourceKey,
                    IngestedAtUtc = now,
                };
                db.Ratings.Add(rating);
                all.Add(rating);
                bySourceKey[r.SourceKey] = rating;
                added++;
            }

            // Merge: a later fetch may add detail (the grade can arrive after the target), but never erases it.
            rating.Firm = firm;
            rating.Analyst = analyst ?? rating.Analyst;
            if (r.RatingTo is not null)
            {
                rating.RatingTo = r.RatingTo;
                rating.RatingFrom = r.RatingFrom ?? rating.RatingFrom;
                rating.RatingCarriedForward = false;
            }
            rating.RawAction = r.Action ?? rating.RawAction;
            rating.PriceTargetTo = r.PriceTarget ?? rating.PriceTargetTo;
            rating.PriceAtCall ??= r.PriceWhenPosted;
        }

        var closes = await db.PriceSnapshots
            .Where(p => p.StockId == stock.Id)
            .OrderBy(p => p.Date)
            .Select(p => new { p.Date, p.Close })
            .ToListAsync(ct);

        DeriveFields(all, closes.Select(c => c.Date).ToArray(), closes.Select(c => c.Close).ToArray());

        var updated = db.ChangeTracker.Entries<Rating>().Count(e => e.State == EntityState.Modified);
        await db.SaveChangesAsync(ct);
        return (added, updated);
    }

    /// <summary>
    /// Fills the fields that depend on a rating's predecessors: carried-forward rating, category,
    /// previous target, action, and price at call. Runs per source, in date order.
    /// </summary>
    internal static void DeriveFields(IEnumerable<Rating> ratings, DateOnly[] closeDates, double[] closes)
    {
        foreach (var source in ratings.GroupBy(r => r.Source))
        {
            var lastGrade = new Dictionary<string, string>();   // per firm
            var lastTarget = new Dictionary<string, double>();  // per analyst, or per firm when no analyst

            foreach (var r in source.OrderBy(r => r.Date).ThenBy(r => r.SourceKey, StringComparer.Ordinal))
            {
                var firmKey = r.Firm.NormalizedName;
                var callerKey = r.Analyst is { } a ? $"{firmKey}|{a.NormalizedName}" : firmKey;

                if (r.RatingCarriedForward || r.RatingTo is null)
                {
                    var prior = lastGrade.GetValueOrDefault(firmKey);
                    r.RatingTo = prior;
                    r.RatingCarriedForward = prior is not null;
                }
                else
                {
                    lastGrade[firmKey] = r.RatingTo;
                }

                r.Category = RatingNormalizer.ToCategory(r.RatingTo);

                double? previousTarget = lastTarget.TryGetValue(callerKey, out var t) ? t : null;
                r.PriceTargetFrom = r.PriceTargetTo is null ? null : previousTarget;
                if (r.PriceTargetTo is { } target) lastTarget[callerKey] = target;

                r.Action = RatingNormalizer.ToAction(r.RawAction, previousTarget, r.PriceTargetTo);
                r.PriceAtCall = CloseOnOrBefore(closeDates, closes, r.Date) ?? r.PriceAtCall;
            }
        }
    }

    private static double? CloseOnOrBefore(DateOnly[] dates, double[] closes, DateOnly date)
    {
        var i = Array.BinarySearch(dates, date);
        if (i >= 0) return closes[i];
        i = ~i - 1; // index of the last date before `date`
        return i >= 0 ? closes[i] : null;
    }

    private DateTime UtcNow() => time.GetUtcNow().UtcDateTime;
    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
