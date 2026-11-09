using System.ComponentModel.DataAnnotations;
using FinancialAnalystsAPI.Models;

namespace FinancialAnalystsAPI.Api;

public sealed class StockListQuery : PagedQuery
{
    /// <summary>Exact sector, e.g. "Technology" (case-insensitive).</summary>
    [StringLength(100)]
    public string? Sector { get; set; }

    /// <summary>Matches ticker or company name.</summary>
    [StringLength(100)]
    public string? Search { get; set; }

    /// <summary>Include stocks no longer in the tracked universe.</summary>
    public bool IncludeUntracked { get; set; }
}

public sealed class NameListQuery : PagedQuery
{
    [StringLength(100)]
    public string? Search { get; set; }

    public int? FirmId { get; set; }
}

/// <summary>Filters shared by every ratings list and the activity feed.</summary>
public class RatingListQuery : PagedQuery, IValidatableObject
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    /// <summary>Repeatable: <c>?action=Upgrade&amp;action=Downgrade</c>.</summary>
    public RatingAction[]? Action { get; set; }

    public RatingCategory? Category { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From is { } from && To is { } to && from > to)
            yield return new ValidationResult("'from' must be on or before 'to'.", [nameof(From), nameof(To)]);
    }
}

public sealed class FeedQuery : RatingListQuery
{
    [RegularExpression(ApiControllerBase.TickerPattern)]
    public string? Ticker { get; set; }

    [StringLength(100)]
    public string? Sector { get; set; }

    public int? FirmId { get; set; }

    public int? AnalystId { get; set; }

    /// <summary>
    /// The feed shows rating changes (upgrades, downgrades, initiations) by default.
    /// Set to include maintained and reiterated calls and target-only moves too.
    /// </summary>
    public bool IncludeAll { get; set; }
}

public sealed class PriceQuery : IValidatableObject
{
    public const int MaxDays = 3660;

    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From is { } from && To is { } to)
        {
            if (from > to)
                yield return new ValidationResult("'from' must be on or before 'to'.", [nameof(From), nameof(To)]);
            else if (to.DayNumber - from.DayNumber > MaxDays)
                yield return new ValidationResult($"The range can span at most {MaxDays} days.", [nameof(From), nameof(To)]);
        }
    }
}

public sealed class ConsensusQuery
{
    /// <summary>Defaults to today (UTC).</summary>
    public DateOnly? AsOf { get; set; }

    /// <summary>Only calls made within this many days before <see cref="AsOf"/> count. Default 90.</summary>
    [Range(1, 730)]
    public int LookbackDays { get; set; } = 90;
}

public sealed class LeaderboardQuery : PagedQuery
{
    /// <summary>Outcome horizon in months (one of Scoring:HorizonsMonths). Defaults to 12.</summary>
    public int? Horizon { get; set; }

    /// <summary>Score only calls on stocks in this sector.</summary>
    [StringLength(100)]
    public string? Sector { get; set; }

    /// <summary>Analysts only: limit to one firm.</summary>
    public int? FirmId { get; set; }

    /// <summary>Minimum matured calls to be ranked. Defaults to Scoring:MinCallsToRank (10).</summary>
    [Range(1, 10000)]
    public int? MinCalls { get; set; }

    /// <summary>Months of month-end score history per row, for sparklines. 0 to omit.</summary>
    [Range(0, 36)]
    public int TrendMonths { get; set; } = 12;
}

public sealed class ScoreCardQuery
{
    /// <summary>Horizon for the trend line. Defaults to 12.</summary>
    public int? TrendHorizon { get; set; }

    [Range(0, 60)]
    public int TrendMonths { get; set; } = 24;
}

public sealed class OutcomeQuery : PagedQuery
{
    /// <summary>Defaults to 12.</summary>
    public int? Horizon { get; set; }

    /// <summary>Only right (true) or wrong (false) calls.</summary>
    public bool? Hit { get; set; }
}

public sealed class ConsensusHistoryQuery
{
    [Range(1, 60)]
    public int Months { get; set; } = 12;

    [Range(1, 730)]
    public int LookbackDays { get; set; } = 90;
}
