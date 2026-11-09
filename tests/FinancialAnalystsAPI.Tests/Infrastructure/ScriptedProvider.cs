using FinancialAnalystsAPI.Providers;

namespace FinancialAnalystsAPI.Tests.Infrastructure;

/// <summary>An <see cref="IAnalystDataProvider"/> whose responses each test sets directly.</summary>
public sealed class ScriptedProvider : IAnalystDataProvider
{
    public string Name => "scripted";

    public Func<string, ProviderStockProfile?> Profile { get; set; } =
        t => new ProviderStockProfile(t, $"{t} Corp", "Technology", "Software", "NASDAQ");

    public Func<string, DateOnly, IReadOnlyList<ProviderRating>> Ratings { get; set; } = (_, _) => [];

    public Func<string, DateOnly, DateOnly, IReadOnlyList<ProviderPrice>> Prices { get; set; } = (_, _, _) => [];

    public Task<ProviderStockProfile?> GetStockProfileAsync(string ticker, CancellationToken ct) =>
        Task.FromResult(Profile(ticker));

    public Task<IReadOnlyList<ProviderRating>> GetRatingsAsync(string ticker, DateOnly since, CancellationToken ct) =>
        Task.FromResult(Ratings(ticker, since));

    public Task<IReadOnlyList<ProviderPrice>> GetDailyPricesAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct) =>
        Task.FromResult(Prices(ticker, from, to));
}
