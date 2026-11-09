namespace FinancialAnalystsAPI.Models;

/// <summary>Audit row written by every ingestion run.</summary>
public class IngestionRun
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public bool Backfill { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public IngestionStatus Status { get; set; }
    public int StocksProcessed { get; set; }
    public int PricesAdded { get; set; }
    public int RatingsAdded { get; set; }
    public int RatingsUpdated { get; set; }

    /// <summary>Per-ticker failures, one per line.</summary>
    public string? Error { get; set; }
}
