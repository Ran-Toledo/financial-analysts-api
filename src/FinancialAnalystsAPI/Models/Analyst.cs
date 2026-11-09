namespace FinancialAnalystsAPI.Models;

/// <summary>An individual analyst. Sectors covered are derived from the stocks in <see cref="Ratings"/>.</summary>
public class Analyst
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Lower-cased, punctuation-free name used to match provider data. Unique per firm.</summary>
    public string NormalizedName { get; set; } = string.Empty;

    public int FirmId { get; set; }
    public Firm Firm { get; set; } = null!;

    /// <summary>Hand-entered rating from the original CRUD API. Replaced by a computed score in Phase 3.</summary>
    public double? ManualRating { get; set; }

    public List<Rating> Ratings { get; set; } = [];
}
