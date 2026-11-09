namespace FinancialAnalystsAPI.Models;

/// <summary>Daily close for a stock. Key: (<see cref="StockId"/>, <see cref="Date"/>).</summary>
public class PriceSnapshot
{
    public int StockId { get; set; }
    public Stock Stock { get; set; } = null!;

    public DateOnly Date { get; set; }

    /// <summary>Split-adjusted close.</summary>
    public double Close { get; set; }

    /// <summary>Split- and dividend-adjusted close, when the provider supplies it.</summary>
    public double? AdjClose { get; set; }
}
