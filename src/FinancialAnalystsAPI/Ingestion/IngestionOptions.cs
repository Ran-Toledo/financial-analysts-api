using System.ComponentModel.DataAnnotations;

namespace FinancialAnalystsAPI.Ingestion;

public sealed class IngestionOptions
{
    public const string Section = "Ingestion";

    /// <summary>Runs the daily background job. The <c>ingest</c> command works either way.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Daily run time in UTC, after US market close (16:00 ET is 20:00–21:00 UTC).</summary>
    public TimeOnly RunAtUtc { get; set; } = new(22, 30);

    /// <summary>Also run once shortly after the app starts.</summary>
    public bool RunOnStartup { get; set; }

    /// <summary>How far back a ticker's first ingestion (or a <c>--backfill</c>) fetches.</summary>
    [Range(1, 30)]
    public int BackfillYears { get; set; } = 3;

    /// <summary>Re-fetch this many days before the newest stored rating, to pick up late-arriving items.</summary>
    [Range(0, 90)]
    public int RatingsOverlapDays { get; set; } = 7;
}

public sealed class TrackingOptions
{
    public const string Section = "Tracking";

    /// <summary>The tracked universe. The free FMP tier (250 calls/day) supports roughly 25 tickers.</summary>
    [Required, MinLength(1)]
    public string[] Tickers { get; set; } = [];

    /// <summary>Ingested like any ticker, and used for excess returns in Phase 3.</summary>
    [Required, RegularExpression(@"^[A-Za-z.\-]{1,10}$")]
    public string BenchmarkTicker { get; set; } = "SPY";

    public IReadOnlyList<string> AllTickers() =>
        Tickers.Append(BenchmarkTicker)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
}
