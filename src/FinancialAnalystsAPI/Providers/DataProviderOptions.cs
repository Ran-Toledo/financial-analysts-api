using System.ComponentModel.DataAnnotations;

namespace FinancialAnalystsAPI.Providers;

public sealed class DataProviderOptions
{
    public const string Section = "DataProvider";
    public const string Fmp = "Fmp";
    public const string Fake = "Fake";

    /// <summary>"Fmp" for Financial Modeling Prep, or "Fake" for offline synthetic data.</summary>
    [Required, AllowedValues(Fmp, Fake)]
    public string Name { get; set; } = Fmp;
}
