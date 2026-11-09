namespace FinancialAnalystsAPI.Models;

/// <summary>
/// How one call (a <see cref="Models.Rating"/> selected by the scoring rules) turned out after one horizon.
/// Derived data: rebuilt from ratings and prices after every ingestion run.
/// </summary>
public class CallOutcome
{
    public int RatingId { get; set; }
    public Rating Rating { get; set; } = null!;

    public int HorizonMonths { get; set; }
    public DateOnly EndDate { get; set; }
    public RatingCategory Category { get; set; }

    /// <summary>Unadjusted closes at the call and at the horizon end.</summary>
    public double StartPrice { get; set; }
    public double EndPrice { get; set; }

    /// <summary>Total return (adjusted closes) of the stock and the benchmark, as fractions.</summary>
    public double Return { get; set; }
    public double BenchmarkReturn { get; set; }
    public double ExcessReturn { get; set; }

    /// <summary>Excess return in the direction of the call; null for Hold.</summary>
    public double? DirectionalExcess { get; set; }

    public bool Hit { get; set; }

    public double? Target { get; set; }
    public bool? TargetHit { get; set; }
    public double? TargetError { get; set; }

    public DateTime ComputedAtUtc { get; set; }
}
