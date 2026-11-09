using System.ComponentModel.DataAnnotations;

namespace FinancialAnalystsAPI.Providers.Fmp;

/// <summary>
/// Bound from the "Fmp" section. Keep <see cref="ApiKey"/> out of appsettings: use
/// <c>dotnet user-secrets set "Fmp:ApiKey" "..."</c> locally and the <c>Fmp__ApiKey</c> environment variable in deployment.
/// </summary>
public sealed class FmpOptions
{
    public const string Section = "Fmp";

    [Required, Url]
    public string BaseUrl { get; set; } = "https://financialmodelingprep.com/stable/";

    /// <summary>Required when <c>DataProvider:Name</c> is "Fmp" (validated at startup).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Client-side cap on calls per UTC day, kept below the plan's limit (free tier: 250).</summary>
    [Range(1, 1_000_000)]
    public int DailyCallBudget { get; set; } = 240;

    [Range(1, 1000)]
    public int PriceTargetPageSize { get; set; } = 100;

    /// <summary>Upper bound on price-target-news pages per ticker per run, to protect the budget during backfill.</summary>
    [Range(1, 1000)]
    public int MaxPriceTargetPages { get; set; } = 10;
}
