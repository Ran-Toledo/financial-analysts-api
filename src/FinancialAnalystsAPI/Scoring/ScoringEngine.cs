using FinancialAnalystsAPI.Models;

namespace FinancialAnalystsAPI.Scoring;

/// <summary>A rating as the engine sees it.</summary>
public sealed record RatingRow(
    int RatingId, string Source, int StockId, int FirmId, int? AnalystId, DateOnly Date, RatingCategory? Category, double? Target);

/// <summary>A rating that counts as a call (see <see cref="ScoringEngine.SelectCalls"/>).</summary>
public sealed record ScoringCall(int RatingId, int StockId, DateOnly Date, RatingCategory Category, double? Target);

/// <summary>What happened after one call over one horizon.</summary>
/// <param name="DirectionalExcess">ExcessReturn for Buy, −ExcessReturn for Sell, null for Hold.</param>
public sealed record OutcomeResult(
    int RatingId,
    int HorizonMonths,
    DateOnly EndDate,
    RatingCategory Category,
    double StartPrice,
    double EndPrice,
    double Return,
    double BenchmarkReturn,
    double ExcessReturn,
    double? DirectionalExcess,
    bool Hit,
    double? Target,
    bool? TargetHit,
    double? TargetError);

/// <summary>Daily prices for one stock, with lookups by date.</summary>
public sealed class PriceSeries
{
    private readonly DateOnly[] _dates;
    private readonly double[] _closes;
    private readonly double[] _adjusted;

    /// <param name="points">Any order. <c>AdjClose</c> falls back to <c>Close</c> when missing.</param>
    public PriceSeries(IEnumerable<(DateOnly Date, double Close, double? AdjClose)> points)
    {
        var sorted = points.OrderBy(p => p.Date).ToArray();
        _dates = sorted.Select(p => p.Date).ToArray();
        _closes = sorted.Select(p => p.Close).ToArray();
        _adjusted = sorted.Select(p => p.AdjClose ?? p.Close).ToArray();
    }

    public DateOnly? LastDate => _dates.Length == 0 ? null : _dates[^1];

    /// <summary>Index of the last trading day on or before <paramref name="date"/>, or −1.</summary>
    private int IndexOnOrBefore(DateOnly date)
    {
        var i = Array.BinarySearch(_dates, date);
        return i >= 0 ? i : ~i - 1;
    }

    /// <summary>Split- and dividend-adjusted close on or before <paramref name="date"/>, if within <paramref name="maxStaleDays"/>.</summary>
    public (DateOnly Date, double Adjusted, double Close)? OnOrBefore(DateOnly date, int maxStaleDays = 7)
    {
        var i = IndexOnOrBefore(date);
        if (i < 0 || date.DayNumber - _dates[i].DayNumber > maxStaleDays) return null;
        return (_dates[i], _adjusted[i], _closes[i]);
    }

    /// <summary>Highest and lowest unadjusted close in (<paramref name="after"/>, <paramref name="through"/>].</summary>
    public (double High, double Low)? RangeBetween(DateOnly after, DateOnly through)
    {
        var start = IndexOnOrBefore(after) + 1;
        var end = IndexOnOrBefore(through);
        if (start > end) return null;

        double high = double.MinValue, low = double.MaxValue;
        for (var i = start; i <= end; i++)
        {
            high = Math.Max(high, _closes[i]);
            low = Math.Min(low, _closes[i]);
        }
        return (high, low);
    }
}

/// <summary>Pure scoring rules: which ratings are calls, and how a call turned out.</summary>
public static class ScoringEngine
{
    /// <summary>
    /// Per contributor (analyst, or firm when there is no analyst) and stock, in date order, a rating is a call when it is
    /// the first one seen, changes the Buy/Hold/Sell category, changes the price target, or reaffirms the opinion at least
    /// <paramref name="reaffirmAfterDays"/> after the last counted call. Ratings with no category are skipped.
    /// </summary>
    public static List<ScoringCall> SelectCalls(IEnumerable<RatingRow> ratings, int reaffirmAfterDays)
    {
        var calls = new List<ScoringCall>();

        var series = ratings
            .Where(r => r.Category is not null)
            .GroupBy(r => (r.Source, r.StockId, Contributor: r.AnalystId is { } a ? (Kind: 'a', Id: a) : (Kind: 'f', Id: r.FirmId)));

        foreach (var group in series)
        {
            ScoringCall? last = null;
            foreach (var r in group.OrderBy(r => r.Date).ThenBy(r => r.RatingId))
            {
                var isCall = last is null
                    || r.Category != last.Category
                    || TargetChanged(last.Target, r.Target)
                    || r.Date.DayNumber - last.Date.DayNumber >= reaffirmAfterDays;
                if (!isCall) continue;

                // A target-less reiteration keeps the previous target standing.
                last = new ScoringCall(r.RatingId, r.StockId, r.Date, r.Category!.Value, r.Target ?? last?.Target);
                calls.Add(last);
            }
        }

        return calls;
    }

    /// <summary>Median absolute excess return, in percent: the adaptive Hold band for one horizon.</summary>
    public static double AdaptiveHoldBandPct(IEnumerable<OutcomeResult> outcomes)
    {
        var gaps = outcomes.Select(o => Math.Abs(o.ExcessReturn)).Order().ToArray();
        if (gaps.Length == 0) return 0;
        var mid = gaps.Length / 2;
        var median = gaps.Length % 2 == 1 ? gaps[mid] : (gaps[mid - 1] + gaps[mid]) / 2;
        return median * 100;
    }

    /// <summary>Re-marks Hold outcomes as right or wrong against <paramref name="holdBandPct"/>.</summary>
    public static OutcomeResult ApplyHoldBand(OutcomeResult outcome, double holdBandPct) =>
        outcome.Category == RatingCategory.Hold
            ? outcome with { Hit = Math.Abs(outcome.ExcessReturn) <= holdBandPct / 100 }
            : outcome;

    private static bool TargetChanged(double? previous, double? next) =>
        next is { } n && (previous is not { } p || Math.Abs(n - p) > p * 0.005);

    /// <summary>
    /// The call's outcome after <paramref name="horizonMonths"/>, or null when it hasn't matured (the horizon ends after the
    /// last stored price of the stock or benchmark) or prices are missing around the start or end.
    /// Returns use adjusted closes; target checks use unadjusted closes, the terms targets are quoted in.
    /// </summary>
    public static OutcomeResult? Evaluate(
        ScoringCall call, int horizonMonths, PriceSeries stock, PriceSeries? benchmark, double holdBandPct)
    {
        var endDate = call.Date.AddMonths(horizonMonths);
        if (stock.LastDate is not { } stockLast || endDate > stockLast) return null;
        if (benchmark is not null && (benchmark.LastDate is not { } benchLast || endDate > benchLast)) return null;

        if (stock.OnOrBefore(call.Date) is not { } start || stock.OnOrBefore(endDate) is not { } end) return null;

        var ret = end.Adjusted / start.Adjusted - 1;

        var benchmarkReturn = 0.0;
        if (benchmark is not null)
        {
            if (benchmark.OnOrBefore(call.Date) is not { } bStart || benchmark.OnOrBefore(endDate) is not { } bEnd) return null;
            benchmarkReturn = bEnd.Adjusted / bStart.Adjusted - 1;
        }

        var excess = ret - benchmarkReturn;
        double? directional = call.Category switch
        {
            RatingCategory.Buy => excess,
            RatingCategory.Sell => -excess,
            _ => null,
        };
        var hit = call.Category switch
        {
            RatingCategory.Buy => excess > 0,
            RatingCategory.Sell => excess < 0,
            _ => Math.Abs(excess) <= holdBandPct / 100,
        };

        bool? targetHit = null;
        double? targetError = null;
        if (call.Target is { } target)
        {
            var range = stock.RangeBetween(call.Date, endDate);
            targetHit = range is { } r && (target >= start.Close ? r.High >= target : r.Low <= target);
            targetError = Math.Abs(end.Close - target) / start.Close;
        }

        return new OutcomeResult(call.RatingId, horizonMonths, endDate, call.Category, start.Close, end.Close,
            ret, benchmarkReturn, excess, directional, hit, call.Target, targetHit, targetError);
    }
}
