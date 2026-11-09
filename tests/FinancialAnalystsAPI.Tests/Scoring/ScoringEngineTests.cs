using FinancialAnalystsAPI.Models;
using FinancialAnalystsAPI.Scoring;

namespace FinancialAnalystsAPI.Tests.Scoring;

public class ScoringEngineTests
{
    private static readonly DateOnly D0 = new(2025, 1, 6); // a Monday

    private static RatingRow Row(int id, int daysAfter, RatingCategory? category, double? target = null, int firm = 1, int? analyst = null, int stock = 1) =>
        new(id, "test", stock, firm, analyst, D0.AddDays(daysAfter), category, target);

    /// <summary>Daily series from D0, one price per weekday, from a function of the day offset.</summary>
    private static PriceSeries Series(int days, Func<int, double> price) =>
        new(Enumerable.Range(0, days)
            .Select(i => D0.AddDays(i))
            .Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            .Select(d => (d, price(d.DayNumber - D0.DayNumber), (double?)null)));

    [Fact]
    public void SelectCalls_CountsFirstChangesAndSpacedReaffirmations()
    {
        var calls = ScoringEngine.SelectCalls(
        [
            Row(1, 0, RatingCategory.Buy),               // first seen
            Row(2, 30, RatingCategory.Buy),              // reaffirmed too soon: skipped
            Row(3, 60, RatingCategory.Hold),             // category change
            Row(4, 100, RatingCategory.Hold),            // 40 days after last counted: skipped
            Row(5, 150, RatingCategory.Hold),            // 90 days after last counted: reaffirmation
            Row(6, 160, RatingCategory.Hold, target: 50),// new target
            Row(7, 170, RatingCategory.Hold, target: 50.1), // within 0.5%: not a change
            Row(8, 180, category: null),                 // unrecognised rating: skipped
        ], reaffirmAfterDays: 90);

        Assert.Equal([1, 3, 5, 6], calls.Select(c => c.RatingId));
    }

    [Fact]
    public void SelectCalls_TracksEachContributorAndStockSeparately()
    {
        var calls = ScoringEngine.SelectCalls(
        [
            Row(1, 0, RatingCategory.Buy, firm: 1),
            Row(2, 10, RatingCategory.Buy, firm: 2),             // different firm: its own first call
            Row(3, 20, RatingCategory.Buy, firm: 1, analyst: 9), // named analyst at firm 1: its own series
            Row(4, 30, RatingCategory.Buy, firm: 1, stock: 2),   // different stock
            Row(5, 40, RatingCategory.Buy, firm: 1),             // firm 1 / stock 1 again, too soon
        ], reaffirmAfterDays: 90);

        Assert.Equal([1, 2, 3, 4], calls.Select(c => c.RatingId).Order());
    }

    [Fact]
    public void Evaluate_MeasuresExcessReturnAgainstBenchmark()
    {
        var stock = Series(500, d => 100 + d * 0.1);   // +36.5% over 365 days
        var spy = Series(500, d => 100 + d * 0.05);    // +18.25%
        var call = new ScoringCall(1, 1, D0, RatingCategory.Buy, null);

        var r = ScoringEngine.Evaluate(call, 12, stock, spy, holdBandPct: 5)!;

        Assert.Equal(D0.AddMonths(12), r.EndDate);
        Assert.Equal(0.365, r.Return, 1e-9);
        Assert.Equal(0.1825, r.BenchmarkReturn, 1e-9);
        Assert.Equal(0.1825, r.ExcessReturn, 1e-9);
        Assert.Equal(r.ExcessReturn, r.DirectionalExcess);
        Assert.True(r.Hit);
    }

    [Theory]
    [InlineData(RatingCategory.Buy, false)]  // stock lagged the benchmark
    [InlineData(RatingCategory.Sell, true)]
    [InlineData(RatingCategory.Hold, true)]  // lagged by 3%, inside a 5% band
    public void Evaluate_HitDependsOnCategory(RatingCategory category, bool expectedHit)
    {
        var stock = Series(400, d => d >= 365 ? 100 : 100 + d * 0.01);  // ends flat
        var spy = Series(400, d => d >= 365 ? 103 : 100 + d * 0.005);   // ends +3%
        var r = ScoringEngine.Evaluate(new ScoringCall(1, 1, D0, category, null), 12, stock, spy, holdBandPct: 5)!;

        Assert.Equal(expectedHit, r.Hit);
        Assert.Equal(category == RatingCategory.Sell ? 0.03 : category == RatingCategory.Buy ? -0.03 : (double?)null,
            r.DirectionalExcess is { } d ? Math.Round(d, 4) : null);
    }

    [Fact]
    public void Evaluate_ReturnsNullUntilTheHorizonHasPrices()
    {
        var stock = Series(200, _ => 100);
        var call = new ScoringCall(1, 1, D0, RatingCategory.Buy, null);

        Assert.NotNull(ScoringEngine.Evaluate(call, 3, stock, null, 5));
        Assert.Null(ScoringEngine.Evaluate(call, 12, stock, null, 5));                 // not matured
        Assert.Null(ScoringEngine.Evaluate(call, 3, stock, Series(30, _ => 100), 5));  // benchmark too short
        Assert.Null(ScoringEngine.Evaluate(call with { Date = D0.AddDays(-30) }, 3, stock, null, 5)); // no price at the call
    }

    [Fact]
    public void Evaluate_TargetHit_UsesTheRangeWithinTheHorizon()
    {
        // Rises to 130 by day 100, then falls back to 110.
        var stock = Series(400, d => d <= 100 ? 100 + d * 0.3 : Math.Max(110, 130 - (d - 100) * 0.5));

        var reached = ScoringEngine.Evaluate(new ScoringCall(1, 1, D0, RatingCategory.Buy, 125), 12, stock, null, 5)!;
        var missed = ScoringEngine.Evaluate(new ScoringCall(2, 1, D0, RatingCategory.Buy, 140), 12, stock, null, 5)!;

        Assert.True(reached.TargetHit);
        Assert.False(missed.TargetHit);
        Assert.Equal(Math.Abs(110 - 140) / 100.0, missed.TargetError!.Value, 3);
    }

    [Fact]
    public void AdaptiveHoldBand_IsTheMedianAbsoluteExcess()
    {
        OutcomeResult O(double excess, RatingCategory c = RatingCategory.Buy) =>
            new(1, 12, D0, c, 100, 100, excess, 0, excess, null, false, null, null, null);

        var band = ScoringEngine.AdaptiveHoldBandPct([O(-0.30), O(0.05), O(0.10), O(-0.20)]);

        Assert.Equal(15, band, 6); // median of 5, 10, 20, 30
        Assert.True(ScoringEngine.ApplyHoldBand(O(0.12, RatingCategory.Hold), band).Hit);
        Assert.False(ScoringEngine.ApplyHoldBand(O(-0.16, RatingCategory.Hold), band).Hit);
        Assert.False(ScoringEngine.ApplyHoldBand(O(0.12, RatingCategory.Buy), band).Hit); // only Holds are re-marked
    }
}
