using FinancialAnalystsAPI.Data;
using FinancialAnalystsAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancialAnalystsAPI.Consensus;

/// <summary>
/// Street consensus for a stock: each contributor's standing call within a lookback window.
/// A contributor is an analyst, or the firm when the provider names no analyst.
/// </summary>
public sealed class ConsensusService(AnalystsDbContext db, TimeProvider time)
{
    public async Task<ConsensusDto?> GetAsync(string ticker, DateOnly? asOf, int lookbackDays, CancellationToken ct)
    {
        var stock = await db.Stocks.Where(s => s.Ticker == ticker).Select(s => new { s.Id, s.Ticker }).SingleOrDefaultAsync(ct);
        if (stock is null) return null;

        var date = asOf ?? Today();
        var calls = await LoadCallsAsync(stock.Id, date.AddDays(-lookbackDays), date, ct);
        var close = await db.PriceSnapshots
            .Where(p => p.StockId == stock.Id && p.Date <= date)
            .OrderByDescending(p => p.Date)
            .Select(p => new { p.Date, p.Close })
            .FirstOrDefaultAsync(ct);

        var snapshot = Compute(calls, date, lookbackDays);
        return new ConsensusDto(
            stock.Ticker, date, lookbackDays, snapshot.Contributors, snapshot.Buy, snapshot.Hold, snapshot.Sell,
            snapshot.Score, snapshot.Rating, SummarizeTargets(snapshot.Targets, close?.Close), close?.Close, close?.Date);
    }

    /// <summary>Consensus at each of the last <paramref name="months"/> month-ends, with today as the final point.</summary>
    public async Task<ConsensusHistoryDto?> GetHistoryAsync(string ticker, int months, int lookbackDays, CancellationToken ct)
    {
        var stock = await db.Stocks.Where(s => s.Ticker == ticker).Select(s => new { s.Id, s.Ticker }).SingleOrDefaultAsync(ct);
        if (stock is null) return null;

        var dates = MonthEnds(Today(), months);
        var (first, last) = (dates[0], dates[^1]);
        var priceFrom = first.AddDays(-10); // so the first point has a close even after a holiday
        var calls = await LoadCallsAsync(stock.Id, first.AddDays(-lookbackDays), last, ct);
        var prices = await db.PriceSnapshots
            .Where(p => p.StockId == stock.Id && p.Date >= priceFrom && p.Date <= last)
            .OrderBy(p => p.Date)
            .Select(p => new { p.Date, p.Close })
            .ToListAsync(ct);

        var points = dates.Select(date =>
        {
            var s = Compute(calls, date, lookbackDays);
            var close = prices.LastOrDefault(p => p.Date <= date)?.Close;
            return new ConsensusPointDto(date, s.Contributors, s.Buy, s.Hold, s.Sell, s.Score, s.Rating,
                s.Targets.Count == 0 ? null : Math.Round(s.Targets.Average(), 2), close);
        }).ToList();

        return new ConsensusHistoryDto(stock.Ticker, lookbackDays, points);
    }

    /// <summary>
    /// Each contributor's latest categorised call in (<paramref name="asOf"/> − lookback, <paramref name="asOf"/>]
    /// counts once; their latest target in the window, if any, feeds the target summary.
    /// </summary>
    public static ConsensusSnapshot Compute(IEnumerable<ConsensusCall> calls, DateOnly asOf, int lookbackDays)
    {
        var from = asOf.AddDays(-lookbackDays);
        var byContributor = calls
            .Where(c => c.Date > from && c.Date <= asOf)
            .GroupBy(c => c.AnalystId is { } a ? (Kind: 'a', Id: a) : (Kind: 'f', Id: c.FirmId))
            .ToList();

        var latestCategories = byContributor
            .Select(g => g.Where(c => c.Category is not null).OrderByDescending(c => c.Date).Select(c => c.Category).FirstOrDefault())
            .Where(c => c is not null)
            .Select(c => c!.Value)
            .ToList();

        var targets = byContributor
            .Select(g => g.Where(c => c.Target is not null).OrderByDescending(c => c.Date).Select(c => c.Target).FirstOrDefault())
            .Where(t => t is not null)
            .Select(t => t!.Value)
            .ToList();

        var buy = latestCategories.Count(c => c == RatingCategory.Buy);
        var hold = latestCategories.Count(c => c == RatingCategory.Hold);
        var sell = latestCategories.Count(c => c == RatingCategory.Sell);
        var total = buy + hold + sell;

        double? score = total == 0 ? null : Math.Round((buy * 1.0 + hold * 2.0 + sell * 3.0) / total, 2);
        RatingCategory? rating = score switch
        {
            null => null,
            <= 1.5 => RatingCategory.Buy,
            >= 2.5 => RatingCategory.Sell,
            _ => RatingCategory.Hold,
        };

        return new ConsensusSnapshot(total, buy, hold, sell, score, rating, targets);
    }

    internal static TargetSummaryDto? SummarizeTargets(IReadOnlyList<double> targets, double? lastClose)
    {
        if (targets.Count == 0) return null;

        var sorted = targets.Order().ToArray();
        var mid = sorted.Length / 2;
        var median = sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
        var mean = sorted.Average();
        double? upside = lastClose is > 0 ? Math.Round((mean / lastClose.Value - 1) * 100, 2) : null;

        return new TargetSummaryDto(sorted.Length, Math.Round(mean, 2), Math.Round(median, 2), sorted[^1], sorted[0], upside);
    }

    /// <summary>Month-ends of the previous <paramref name="months"/> − 1 months, then <paramref name="today"/>.</summary>
    internal static DateOnly[] MonthEnds(DateOnly today, int months)
    {
        var result = new List<DateOnly>(months);
        for (var i = months - 1; i >= 1; i--)
        {
            var m = today.AddMonths(-i);
            result.Add(new DateOnly(m.Year, m.Month, DateTime.DaysInMonth(m.Year, m.Month)));
        }
        result.Add(today);
        return result.ToArray();
    }

    private Task<List<ConsensusCall>> LoadCallsAsync(int stockId, DateOnly from, DateOnly to, CancellationToken ct) =>
        db.Ratings
            .Where(r => r.StockId == stockId && r.Date > from && r.Date <= to)
            .Select(r => new ConsensusCall(r.Date, r.FirmId, r.AnalystId, r.Category, r.PriceTargetTo))
            .ToListAsync(ct);

    private DateOnly Today() => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
}
