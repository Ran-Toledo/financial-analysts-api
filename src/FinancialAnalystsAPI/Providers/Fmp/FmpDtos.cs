namespace FinancialAnalystsAPI.Providers.Fmp;

// Response shapes for FMP's "stable" API. Fields are nullable because FMP omits or nulls them freely.
// NOTE: written from FMP's documentation and not yet checked against live responses (see plan step 1.1).

internal sealed record FmpProfile(string? Symbol, string? CompanyName, string? Sector, string? Industry, string? Exchange);

/// <summary>historical-price-eod/full: split-adjusted OHLCV.</summary>
internal sealed record FmpEodPrice(string? Date, double? Close);

/// <summary>historical-price-eod/dividend-adjusted.</summary>
internal sealed record FmpAdjustedPrice(string? Date, double? AdjClose);

/// <summary>grades: firm-level rating changes, with no analyst name.</summary>
internal sealed record FmpGrade(string? Symbol, string? Date, string? GradingCompany, string? PreviousGrade, string? NewGrade, string? Action);

/// <summary>price-target-news: analyst-level price targets, with no rating.</summary>
internal sealed record FmpPriceTargetNews(
    string? Symbol,
    string? PublishedDate,
    string? AnalystName,
    string? AnalystCompany,
    double? PriceTarget,
    double? AdjPriceTarget,
    double? PriceWhenPosted);
