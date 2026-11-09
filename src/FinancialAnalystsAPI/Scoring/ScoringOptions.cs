using System.ComponentModel.DataAnnotations;

namespace FinancialAnalystsAPI.Scoring;

/// <summary>Bound from the "Scoring" section. See the README for how these shape the score.</summary>
public sealed class ScoringOptions
{
    public const string Section = "Scoring";

    private static readonly int[] DefaultHorizons = [3, 6, 12];

    /// <summary>
    /// How far after a call its outcome is measured, in months. Empty by default on purpose: the configuration
    /// binder appends to an array's initial values instead of replacing them. Read <see cref="Horizons"/>.
    /// </summary>
    public int[] HorizonsMonths { get; set; } = [];

    /// <summary><see cref="HorizonsMonths"/> de-duplicated and ascending, or 3, 6 and 12 when none are configured.</summary>
    public int[] Horizons => HorizonsMonths.Length == 0 ? DefaultHorizons : HorizonsMonths.Distinct().Order().ToArray();

    /// <summary>Horizon used when a request doesn't name one.</summary>
    [Range(1, 60)]
    public int DefaultHorizonMonths { get; set; } = 12;

    /// <summary>
    /// A Hold is right when the stock lands within ± this many percent of the benchmark. Leave unset (the default) for
    /// an adaptive band per horizon: the median absolute excess return of all matured calls at that horizon, so a Hold is
    /// right about as often as a random Buy or Sell. A fixed band penalises Holds at long horizons, where single stocks
    /// rarely stay close to the index.
    /// </summary>
    [Range(0.1, 100)]
    public double? HoldBandPct { get; set; }

    /// <summary>A reiterated rating counts as a new call once this many days have passed since the last counted one.</summary>
    [Range(1, 3650)]
    public int ReaffirmAfterDays { get; set; } = 90;

    /// <summary>
    /// Shrinkage strength: a contributor's averages are blended with the population's as if they had this many
    /// extra average calls, so a handful of lucky calls can't top the board.
    /// </summary>
    [Range(0, 1000)]
    public int ShrinkageCalls { get; set; } = 10;

    /// <summary>Minimum matured calls to appear on a leaderboard (requests can override).</summary>
    [Range(1, 10000)]
    public int MinCallsToRank { get; set; } = 10;

    /// <summary>Average directional excess return that maps to the top (and, negated, the bottom) of the return half of the score.</summary>
    [Range(0.01, 5)]
    public double ReturnSpan { get; set; } = 0.20;

    public bool IsHorizon(int months) => Horizons.Contains(months);
}
