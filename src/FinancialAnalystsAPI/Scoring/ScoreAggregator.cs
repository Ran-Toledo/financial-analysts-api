using FinancialAnalystsAPI.Models;

namespace FinancialAnalystsAPI.Scoring;

/// <summary>A stored outcome joined with who made the call and on which stock.</summary>
public sealed record OutcomeRow(
    int RatingId,
    int FirmId,
    int? AnalystId,
    string? Sector,
    int HorizonMonths,
    DateOnly CallDate,
    DateOnly EndDate,
    RatingCategory Category,
    double Return,
    double ExcessReturn,
    double? DirectionalExcess,
    bool Hit,
    bool? TargetHit,
    double? TargetError);

/// <param name="Calls">Matured calls.</param>
/// <param name="HitRate">Share of calls that were right (0–1): Buy beat the benchmark, Sell lagged it, Hold stayed within the band.</param>
/// <param name="AvgExcessReturn">Mean return over the benchmark across all calls, as a fraction.</param>
/// <param name="AvgDirectionalExcess">Mean excess return in the direction of the call, over Buy and Sell calls only.</param>
/// <param name="TargetCalls">Calls with a price target; target fields are null when zero.</param>
/// <param name="Score">0–100: half shrunk hit rate, half shrunk directional excess return.</param>
public sealed record ScoreStats(
    int Calls,
    int DirectionalCalls,
    double HitRate,
    double AvgReturn,
    double AvgExcessReturn,
    double? AvgDirectionalExcess,
    int TargetCalls,
    double? TargetHitRate,
    double? AvgTargetError,
    double Score);

public static class ScoreAggregator
{
    /// <summary>
    /// Stats per subject (firm or analyst id from <paramref name="subject"/>; rows where it returns null are skipped).
    /// Hit rate and directional return are shrunk toward the mean of all <paramref name="rows"/> before scoring:
    /// <c>adjusted = (n·x + k·μ) / (n + k)</c> with k = <see cref="ScoringOptions.ShrinkageCalls"/>.
    /// </summary>
    public static Dictionary<int, ScoreStats> Aggregate(
        IReadOnlyCollection<OutcomeRow> rows, Func<OutcomeRow, int?> subject, ScoringOptions options)
    {
        if (rows.Count == 0) return [];

        var popHit = rows.Average(r => r.Hit ? 1.0 : 0.0);
        var directional = rows.Where(r => r.DirectionalExcess is not null).ToList();
        var popDirectional = directional.Count == 0 ? 0 : directional.Average(r => r.DirectionalExcess!.Value);
        var k = options.ShrinkageCalls;

        var result = new Dictionary<int, ScoreStats>();
        foreach (var group in rows.Where(r => subject(r) is not null).GroupBy(r => subject(r)!.Value))
        {
            var calls = group.ToList();
            var n = calls.Count;
            var hitRate = calls.Average(r => r.Hit ? 1.0 : 0.0);

            var dir = calls.Where(r => r.DirectionalExcess is not null).Select(r => r.DirectionalExcess!.Value).ToList();
            double? avgDir = dir.Count == 0 ? null : dir.Average();

            var targeted = calls.Where(r => r.TargetHit is not null).ToList();

            var adjHit = (n * hitRate + k * popHit) / (n + k);
            var adjDir = (dir.Count * (avgDir ?? 0) + k * popDirectional) / (dir.Count + k == 0 ? 1 : dir.Count + k);
            var returnPart = Math.Clamp(0.5 + adjDir / (2 * options.ReturnSpan), 0, 1);
            var score = Math.Round(100 * (0.5 * adjHit + 0.5 * returnPart), 1, MidpointRounding.AwayFromZero);

            result[group.Key] = new ScoreStats(
                n,
                dir.Count,
                Math.Round(hitRate, 4),
                Math.Round(calls.Average(r => r.Return), 4),
                Math.Round(calls.Average(r => r.ExcessReturn), 4),
                avgDir is null ? null : Math.Round(avgDir.Value, 4),
                targeted.Count,
                targeted.Count == 0 ? null : Math.Round(targeted.Average(r => r.TargetHit!.Value ? 1.0 : 0.0), 4),
                targeted.Count == 0 ? null : Math.Round(targeted.Average(r => r.TargetError ?? 0), 4),
                score);
        }

        return result;
    }

    /// <summary>
    /// Score as of each date in <paramref name="asOfDates"/>, counting only outcomes whose horizon had ended by then.
    /// Population means are recomputed per date, so each point is what the leaderboard would have shown at the time.
    /// </summary>
    public static Dictionary<int, List<TrendPoint>> Trend(
        IReadOnlyCollection<OutcomeRow> rows, Func<OutcomeRow, int?> subject, IReadOnlyList<DateOnly> asOfDates, ScoringOptions options)
    {
        var trend = new Dictionary<int, List<TrendPoint>>();
        foreach (var date in asOfDates)
        {
            var known = rows.Where(r => r.EndDate <= date).ToList();
            foreach (var (id, stats) in Aggregate(known, subject, options))
            {
                if (!trend.TryGetValue(id, out var points)) trend[id] = points = [];
                points.Add(new TrendPoint(date, stats.Score, stats.Calls));
            }
        }
        return trend;
    }
}

public sealed record TrendPoint(DateOnly AsOf, double Score, int Calls);
