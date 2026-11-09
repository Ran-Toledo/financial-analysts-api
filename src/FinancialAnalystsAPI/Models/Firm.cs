namespace FinancialAnalystsAPI.Models;

/// <summary>A research firm or broker (e.g. the "grading company" on a rating).</summary>
public class Firm
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Lower-cased name without punctuation or legal suffixes, used to match provider data.</summary>
    public string NormalizedName { get; set; } = string.Empty;

    public List<Analyst> Analysts { get; set; } = [];
    public List<Rating> Ratings { get; set; } = [];
}
