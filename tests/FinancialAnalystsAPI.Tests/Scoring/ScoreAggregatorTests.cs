using FinancialAnalystsAPI.Models;
using FinancialAnalystsAPI.Scoring;

namespace FinancialAnalystsAPI.Tests.Scoring;

public class ScoreAggregatorTests
{
    private static readonly ScoringOptions Options = new() { ShrinkageCalls = 10, ReturnSpan = 0.20 };
    private static readonly DateOnly D = new(2025, 1, 1);

    private static OutcomeRow Row(int firm, bool hit, double excess, RatingCategory category = RatingCategory.Buy, int endOffset = 0) =>
        new(0, firm, null, "Technology", 12, D, D.AddDays(endOffset), category, excess, excess,
            category == RatingCategory.Hold ? null : category == RatingCategory.Sell ? -excess : excess, hit, null, null);

    [Fact]
    public void Score_BlendsShrunkHitRateAndDirectionalReturn()
    {
        // Firm 1: 10 calls, all right, +10% each. Firm 2: 10 calls, all wrong, −10% each.
        var rows = Enumerable.Range(0, 10).Select(_ => Row(1, true, 0.10))
            .Concat(Enumerable.Range(0, 10).Select(_ => Row(2, false, -0.10)))
            .ToList();

        var stats = ScoreAggregator.Aggregate(rows, r => r.FirmId, Options);

        // Population: hit 0.5, directional 0. Firm 1 shrunk: hit (10·1 + 10·0.5)/20 = 0.75; return (10·0.1)/20 = 0.05 → 0.5 + 0.05/0.4 = 0.625.
        Assert.Equal(68.8, stats[1].Score); // 100 × (0.5·0.75 + 0.5·0.625) = 68.75
        Assert.Equal(31.3, stats[2].Score);
        Assert.Equal((10, 1.0, 0.1), (stats[1].Calls, stats[1].HitRate, stats[1].AvgExcessReturn));
    }

    [Fact]
    public void Shrinkage_KeepsAFewLuckyCallsBelowAConsistentRecord()
    {
        var rows = Enumerable.Range(0, 2).Select(_ => Row(1, true, 0.30))      // 2 calls, both great
            .Concat(Enumerable.Range(0, 40).Select(i => Row(2, i % 4 != 0, 0.08))) // 40 calls, 75% right
            .Concat(Enumerable.Range(0, 40).Select(_ => Row(3, false, -0.05)))
            .ToList();

        var stats = ScoreAggregator.Aggregate(rows, r => r.FirmId, Options);

        Assert.True(stats[2].Score > stats[1].Score);
    }

    [Fact]
    public void Holds_CountForHitRateButNotDirectionalReturn()
    {
        var stats = ScoreAggregator.Aggregate(
            [Row(1, true, 0.50, RatingCategory.Hold), Row(1, true, 0.10)], r => r.FirmId, Options)[1];

        Assert.Equal((2, 1), (stats.Calls, stats.DirectionalCalls));
        Assert.Equal(0.1, stats.AvgDirectionalExcess);
        Assert.Equal(0.3, stats.AvgExcessReturn);
    }

    [Fact]
    public void Trend_OnlyCountsOutcomesKnownByEachDate()
    {
        var rows = new List<OutcomeRow> { Row(1, true, 0.1, endOffset: 10), Row(1, false, -0.1, endOffset: 40) };

        var trend = ScoreAggregator.Trend(rows, r => r.FirmId, [D.AddDays(5), D.AddDays(20), D.AddDays(50)], Options)[1];

        Assert.Equal([(D.AddDays(20), 1), (D.AddDays(50), 2)], trend.Select(p => (p.AsOf, p.Calls)));
    }
}
