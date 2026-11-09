namespace FinancialAnalystsAPI.Providers;

/// <summary>
/// Source of analyst ratings, price targets and daily prices. Implementations return plain records;
/// all mapping to entities and normalisation happens in ingestion.
/// </summary>
public interface IAnalystDataProvider
{
    /// <summary>Short, stable name stored as <c>Rating.Source</c> (e.g. "fmp").</summary>
    string Name { get; }

    Task<ProviderStockProfile?> GetStockProfileAsync(string ticker, CancellationToken ct);

    /// <summary>
    /// Ratings and price targets on or after <paramref name="since"/>, at most one per firm per day.
    /// </summary>
    Task<IReadOnlyList<ProviderRating>> GetRatingsAsync(string ticker, DateOnly since, CancellationToken ct);

    Task<IReadOnlyList<ProviderPrice>> GetDailyPricesAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct);
}

public sealed record ProviderStockProfile(string Ticker, string Name, string? Sector, string? Industry, string? Exchange);

/// <param name="Action">Provider action string, e.g. "upgrade", "downgrade", "init", "maintain".</param>
/// <param name="PriceTarget">Split-adjusted price target, when the call includes one.</param>
/// <param name="SourceKey">Stable, provider-unique key; repeated fetches of the same call must return the same key.</param>
public sealed record ProviderRating(
    DateOnly Date,
    string Firm,
    string? Analyst,
    string? RatingFrom,
    string? RatingTo,
    string? Action,
    double? PriceTarget,
    double? PriceWhenPosted,
    string SourceKey);

public sealed record ProviderPrice(DateOnly Date, double Close, double? AdjClose);

/// <summary>Thrown when the provider's daily call budget is spent; ingestion stops the run cleanly.</summary>
public sealed class ProviderQuotaExceededException(string message) : Exception(message);

/// <summary>
/// Thrown when the provider rejects the credentials. Every later call would fail the same way,
/// so ingestion stops the run instead of spending a call per ticker.
/// </summary>
public sealed class ProviderAuthenticationException(string message) : Exception(message);

/// <summary>
/// Thrown when the subscription doesn't cover a request: either the whole endpoint
/// (<paramref name="symbolOnly"/> false) or just this symbol on an otherwise available endpoint.
/// </summary>
public sealed class ProviderRestrictedException(string message, bool symbolOnly) : Exception(message)
{
    public bool SymbolOnly { get; } = symbolOnly;
}
