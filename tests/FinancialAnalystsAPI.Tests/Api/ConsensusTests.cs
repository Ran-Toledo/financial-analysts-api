using FinancialAnalystsAPI.Consensus;
using FinancialAnalystsAPI.Models;

namespace FinancialAnalystsAPI.Tests.Api;

public class ConsensusTests
{
    private static readonly DateOnly AsOf = new(2026, 11, 9);

    private static ConsensusCall Call(int daysAgo, int firm, int? analyst, RatingCategory? category, double? target = null) =>
        new(AsOf.AddDays(-daysAgo), firm, analyst, category, target);

    [Fact]
    public void CountsEachContributorsLatestCallOnce()
    {
        var snapshot = ConsensusService.Compute(
        [
            Call(80, firm: 1, analyst: null, RatingCategory.Sell),
            Call(10, firm: 1, analyst: null, RatingCategory.Buy),   // firm 1's latest: Buy
            Call(30, firm: 2, analyst: null, RatingCategory.Hold),
            Call(5, firm: 3, analyst: 7, RatingCategory.Sell),      // analyst 7 at firm 3
            Call(4, firm: 3, analyst: 8, RatingCategory.Buy),       // analyst 8 at firm 3 counts separately
        ], AsOf, lookbackDays: 90);

        Assert.Equal((4, 2, 1, 1), (snapshot.Contributors, snapshot.Buy, snapshot.Hold, snapshot.Sell));
        Assert.Equal(1.75, snapshot.Score); // (1 + 1 + 2 + 3) / 4
        Assert.Equal(RatingCategory.Hold, snapshot.Rating);
    }

    [Fact]
    public void IgnoresCallsOutsideTheWindow()
    {
        var snapshot = ConsensusService.Compute(
        [
            Call(91, firm: 1, analyst: null, RatingCategory.Sell), // before the window
            Call(-1, firm: 2, analyst: null, RatingCategory.Sell), // after asOf
            Call(90, firm: 3, analyst: null, RatingCategory.Sell), // exactly asOf − 90: excluded
            Call(89, firm: 4, analyst: null, RatingCategory.Buy),
        ], AsOf, lookbackDays: 90);

        Assert.Equal((1, 1, 0, 0), (snapshot.Contributors, snapshot.Buy, snapshot.Hold, snapshot.Sell));
    }

    [Fact]
    public void UnrecognisedRatings_FallBackToTheContributorsLastKnownCategory()
    {
        var snapshot = ConsensusService.Compute(
        [
            Call(40, firm: 1, analyst: null, RatingCategory.Buy),
            Call(10, firm: 1, analyst: null, category: null),
        ], AsOf, lookbackDays: 90);

        Assert.Equal((1, 1), (snapshot.Contributors, snapshot.Buy));
    }

    [Fact]
    public void Targets_UseEachContributorsLatestTarget()
    {
        var snapshot = ConsensusService.Compute(
        [
            Call(40, firm: 1, analyst: 1, RatingCategory.Buy, target: 100),
            Call(10, firm: 1, analyst: 1, RatingCategory.Buy, target: 120),
            Call(5, firm: 1, analyst: 1, RatingCategory.Buy, target: null), // no new target: 120 stands
            Call(5, firm: 2, analyst: 2, RatingCategory.Hold, target: 90),
            Call(5, firm: 3, analyst: null, RatingCategory.Hold),            // no target at all
        ], AsOf, lookbackDays: 90);

        Assert.Equal([120.0, 90.0], snapshot.Targets.Order().Reverse());
    }

    [Theory]
    [InlineData(new[] { RatingCategory.Buy, RatingCategory.Hold }, RatingCategory.Buy)]   // 1.5 → Buy
    [InlineData(new[] { RatingCategory.Hold, RatingCategory.Sell }, RatingCategory.Sell)] // 2.5 → Sell
    [InlineData(new[] { RatingCategory.Buy, RatingCategory.Sell }, RatingCategory.Hold)]  // 2.0 → Hold
    public void Rating_UsesScoreThresholds(RatingCategory[] categories, RatingCategory expected)
    {
        var calls = categories.Select((c, i) => Call(1, firm: i + 1, analyst: null, c));

        Assert.Equal(expected, ConsensusService.Compute(calls, AsOf, 90).Rating);
    }

    [Fact]
    public void NoCalls_GivesEmptyConsensus()
    {
        var snapshot = ConsensusService.Compute([], AsOf, 90);

        Assert.Equal((0, (double?)null, (RatingCategory?)null), (snapshot.Contributors, snapshot.Score, snapshot.Rating));
        Assert.Null(ConsensusService.SummarizeTargets(snapshot.Targets, 100));
    }

    [Fact]
    public void SummarizeTargets_ComputesStatsAndUpside()
    {
        var odd = ConsensusService.SummarizeTargets([90, 130, 110], lastClose: 100);
        var even = ConsensusService.SummarizeTargets([90, 100, 120, 130], lastClose: null);

        Assert.Equal(new TargetSummaryDto(3, 110, 110, 130, 90, 10), odd);
        Assert.Equal(110, even!.Median);
        Assert.Null(even.UpsidePct);
    }

    [Fact]
    public void MonthEnds_EndWithToday()
    {
        var dates = ConsensusService.MonthEnds(new DateOnly(2026, 3, 15), 4);

        Assert.Equal([new(2025, 12, 31), new(2026, 1, 31), new(2026, 2, 28), new DateOnly(2026, 3, 15)], dates);
    }
}
