namespace FinancialAnalystsAPI.Models;

public class Stock
{
    public int Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Sector { get; set; }
    public string? Industry { get; set; }
    public string? Exchange { get; set; }

    /// <summary>True while the ticker is in the configured tracking universe (<c>Tracking:Tickers</c>).</summary>
    public bool IsTracked { get; set; }

    /// <summary>True for the benchmark used to compute excess returns (<c>Tracking:BenchmarkTicker</c>).</summary>
    public bool IsBenchmark { get; set; }

    public List<Rating> Ratings { get; set; } = [];
    public List<PriceSnapshot> Prices { get; set; } = [];
}
