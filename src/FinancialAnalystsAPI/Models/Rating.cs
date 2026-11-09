namespace FinancialAnalystsAPI.Models;

/// <summary>
/// One firm's call on one stock on one day: a rating change, a price target, or both.
/// Raw provider values are kept alongside the fields derived from them during ingestion.
/// </summary>
public class Rating
{
    public int Id { get; set; }

    public int StockId { get; set; }
    public Stock Stock { get; set; } = null!;

    public int FirmId { get; set; }
    public Firm Firm { get; set; } = null!;

    /// <summary>Null when the provider only names the firm.</summary>
    public int? AnalystId { get; set; }
    public Analyst? Analyst { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>Derived from <see cref="RawAction"/> and the change in price target.</summary>
    public RatingAction Action { get; set; }

    /// <summary>The provider's action string as received (e.g. "upgrade", "maintain").</summary>
    public string? RawAction { get; set; }

    /// <summary>Firm-specific rating strings, e.g. "Equal-Weight" → "Overweight".</summary>
    public string? RatingFrom { get; set; }
    public string? RatingTo { get; set; }

    /// <summary>
    /// True when the provider sent only a price target and <see cref="RatingTo"/> was carried forward
    /// from the firm's previous rating on this stock.
    /// </summary>
    public bool RatingCarriedForward { get; set; }

    /// <summary><see cref="RatingTo"/> normalised to Buy/Hold/Sell; null when the string is unrecognised.</summary>
    public RatingCategory? Category { get; set; }

    /// <summary>The same analyst's (or firm's) previous target on this stock.</summary>
    public double? PriceTargetFrom { get; set; }
    public double? PriceTargetTo { get; set; }

    /// <summary>Close on or before <see cref="Date"/>; falls back to the provider's price when no close is stored.</summary>
    public double? PriceAtCall { get; set; }

    /// <summary>Provider name, e.g. "fmp" or "fake".</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Provider-unique key; (<see cref="Source"/>, <see cref="SourceKey"/>) makes ingestion idempotent.</summary>
    public string SourceKey { get; set; } = string.Empty;

    public DateTime IngestedAtUtc { get; set; }
}
