namespace FinancialAnalystsAPI.Providers.Fake;

/// <summary>
/// Deterministic, offline provider for development and tests (<c>DataProvider:Name = "Fake"</c>).
/// All data is synthetic: prices are a seeded random walk and the firms and analysts are fictional.
/// The same ticker and date always produce the same values, across runs and machines.
/// </summary>
public sealed class FakeAnalystDataProvider(TimeProvider time) : IAnalystDataProvider
{
    public const string ProviderName = "fake";

    /// <summary>Synthetic history starts here, so backfills of any reasonable length have data.</summary>
    public static readonly DateOnly Epoch = new(2015, 1, 2);

    private sealed record FakeFirm(string Name, string Buy, string Hold, string Sell, string[] Analysts);

    private static readonly FakeFirm[] Firms =
    [
        new("Northwind Securities", "Outperform", "Market Perform", "Underperform", ["Avery Lin", "Jordan Patel"]),
        new("Contoso Capital", "Overweight", "Equal-Weight", "Underweight", ["Morgan Reyes"]),
        new("Fabrikam Partners", "Buy", "Neutral", "Sell", ["Casey Okafor", "Riley Novak"]),
        new("Tailspin Research", "Buy", "Hold", "Sell", ["Taylor Brandt"]),
        new("Woodgrove Markets", "Outperform", "Sector Perform", "Underperform", ["Quinn Alvarez"]),
        new("Adatum Equity", "Positive", "Neutral", "Negative", ["Sam Whitfield"]),
    ];

    // Real company names and sectors for the default tracking universe, so sector filters behave sensibly in dev.
    private static readonly Dictionary<string, ProviderStockProfile> Profiles = new (string Ticker, string Name, string Sector, string Industry, string Exchange)[]
    {
        ("AAPL", "Apple Inc.", "Technology", "Consumer Electronics", "NASDAQ"),
        ("MSFT", "Microsoft Corporation", "Technology", "Software - Infrastructure", "NASDAQ"),
        ("NVDA", "NVIDIA Corporation", "Technology", "Semiconductors", "NASDAQ"),
        ("AMD", "Advanced Micro Devices, Inc.", "Technology", "Semiconductors", "NASDAQ"),
        ("INTC", "Intel Corporation", "Technology", "Semiconductors", "NASDAQ"),
        ("CRM", "Salesforce, Inc.", "Technology", "Software - Application", "NYSE"),
        ("AMZN", "Amazon.com, Inc.", "Consumer Cyclical", "Internet Retail", "NASDAQ"),
        ("TSLA", "Tesla, Inc.", "Consumer Cyclical", "Auto Manufacturers", "NASDAQ"),
        ("HD", "The Home Depot, Inc.", "Consumer Cyclical", "Home Improvement Retail", "NYSE"),
        ("GOOGL", "Alphabet Inc.", "Communication Services", "Internet Content & Information", "NASDAQ"),
        ("META", "Meta Platforms, Inc.", "Communication Services", "Internet Content & Information", "NASDAQ"),
        ("NFLX", "Netflix, Inc.", "Communication Services", "Entertainment", "NASDAQ"),
        ("DIS", "The Walt Disney Company", "Communication Services", "Entertainment", "NYSE"),
        ("JPM", "JPMorgan Chase & Co.", "Financial Services", "Banks - Diversified", "NYSE"),
        ("GS", "The Goldman Sachs Group, Inc.", "Financial Services", "Capital Markets", "NYSE"),
        ("V", "Visa Inc.", "Financial Services", "Credit Services", "NYSE"),
        ("UNH", "UnitedHealth Group Incorporated", "Healthcare", "Healthcare Plans", "NYSE"),
        ("JNJ", "Johnson & Johnson", "Healthcare", "Drug Manufacturers - General", "NYSE"),
        ("PFE", "Pfizer Inc.", "Healthcare", "Drug Manufacturers - General", "NYSE"),
        ("XOM", "Exxon Mobil Corporation", "Energy", "Oil & Gas Integrated", "NYSE"),
        ("CVX", "Chevron Corporation", "Energy", "Oil & Gas Integrated", "NYSE"),
        ("PG", "The Procter & Gamble Company", "Consumer Defensive", "Household & Personal Products", "NYSE"),
        ("KO", "The Coca-Cola Company", "Consumer Defensive", "Beverages - Non-Alcoholic", "NYSE"),
        ("WMT", "Walmart Inc.", "Consumer Defensive", "Discount Stores", "NYSE"),
        ("BA", "The Boeing Company", "Industrials", "Aerospace & Defense", "NYSE"),
        ("SPY", "SPDR S&P 500 ETF Trust", "Financial Services", "Asset Management", "AMEX"),
    }.ToDictionary(p => p.Ticker, p => new ProviderStockProfile(p.Ticker, p.Name, p.Sector, p.Industry, p.Exchange),
        StringComparer.OrdinalIgnoreCase);

    public string Name => ProviderName;

    public Task<ProviderStockProfile?> GetStockProfileAsync(string ticker, CancellationToken ct)
    {
        var profile = Profiles.GetValueOrDefault(ticker)
            ?? new ProviderStockProfile(ticker.ToUpperInvariant(), $"{ticker.ToUpperInvariant()} Holdings (synthetic)", "Industrials", "Conglomerates", "NYSE");
        return Task.FromResult<ProviderStockProfile?>(profile);
    }

    public Task<IReadOnlyList<ProviderPrice>> GetDailyPricesAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct)
    {
        IReadOnlyList<ProviderPrice> prices = PriceSeries(ticker, Min(to, Today()))
            .Where(p => p.Date >= from)
            .ToList();
        return Task.FromResult(prices);
    }

    public Task<IReadOnlyList<ProviderRating>> GetRatingsAsync(string ticker, DateOnly since, CancellationToken ct)
    {
        // Analysts don't rate index funds; the benchmark only needs prices.
        if (Etfs.Contains(ticker)) return Task.FromResult<IReadOnlyList<ProviderRating>>([]);

        var today = Today();
        var prices = PriceSeries(ticker, today);
        var ratings = new List<ProviderRating>();
        var symbol = ticker.ToUpperInvariant();

        foreach (var firm in Firms)
        {
            // Each firm covers roughly two thirds of tickers, through one of its analysts.
            var rng = new Random(Seed(symbol, firm.Name));
            if (rng.NextDouble() > 0.66) continue;
            var analyst = firm.Analysts[rng.Next(firm.Analysts.Length)];

            var date = NextWeekday(Epoch.AddDays(rng.Next(0, 120)));
            var category = rng.Next(3); // 0 = Buy, 1 = Hold, 2 = Sell
            string? previousGrade = null;

            while (date <= today)
            {
                var close = PriceOnOrBefore(prices, date);
                if (close is not null)
                {
                    var bias = category switch { 0 => 0.15, 1 => 0.03, _ => -0.10 };
                    var target = Math.Round(close.Value * (1 + bias + (rng.NextDouble() - 0.5) * 0.10), 2);
                    var grade = category switch { 0 => firm.Buy, 1 => firm.Hold, _ => firm.Sell };
                    var action = previousGrade is null ? "init"
                        : grade == previousGrade ? "maintain"
                        : IsBetter(grade, previousGrade, firm) ? "upgrade" : "downgrade";

                    // About one call in five is a target-only note (no rating), exercising the carry-forward path.
                    var targetOnly = previousGrade is not null && action == "maintain" && rng.NextDouble() < 0.2;

                    if (date >= since)
                    {
                        ratings.Add(new ProviderRating(
                            Date: date,
                            Firm: firm.Name,
                            Analyst: analyst,
                            RatingFrom: targetOnly ? null : previousGrade,
                            RatingTo: targetOnly ? null : grade,
                            Action: targetOnly ? null : action,
                            PriceTarget: target,
                            PriceWhenPosted: close,
                            SourceKey: $"{symbol}:{date:yyyy-MM-dd}:{firm.Name.ToLowerInvariant()}"));
                    }

                    previousGrade = grade;
                }

                date = NextWeekday(date.AddDays(rng.Next(30, 120)));
                if (rng.NextDouble() < 0.3) category = Math.Clamp(category + (rng.Next(2) == 0 ? -1 : 1), 0, 2);
            }
        }

        IReadOnlyList<ProviderRating> result = ratings.OrderBy(r => r.Date).ThenBy(r => r.Firm).ToList();
        return Task.FromResult(result);
    }

    private static bool IsBetter(string grade, string previous, FakeFirm firm)
    {
        int Rank(string g) => g == firm.Buy ? 0 : g == firm.Hold ? 1 : 2;
        return Rank(grade) < Rank(previous);
    }

    /// <summary>Weekday-only geometric random walk from <see cref="Epoch"/> to <paramref name="to"/>.</summary>
    private static List<ProviderPrice> PriceSeries(string ticker, DateOnly to)
    {
        var symbol = ticker.ToUpperInvariant();
        var rng = new Random(Seed(symbol, "prices"));
        var price = 20 + rng.NextDouble() * 280;
        var drift = 0.0002 + rng.NextDouble() * 0.0004;
        var series = new List<ProviderPrice>();

        for (var d = Epoch; d <= to; d = d.AddDays(1))
        {
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            var shock = (rng.NextDouble() - 0.5) * 0.04;
            price = Math.Max(1, price * (1 + drift + shock));
            var close = Math.Round(price, 2);
            series.Add(new ProviderPrice(d, close, close));
        }

        return series;
    }

    private static double? PriceOnOrBefore(List<ProviderPrice> prices, DateOnly date)
    {
        var lo = 0;
        var hi = prices.Count - 1;
        double? found = null;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (prices[mid].Date <= date) { found = prices[mid].Close; lo = mid + 1; }
            else hi = mid - 1;
        }
        return found;
    }

    private static readonly HashSet<string> Etfs = new(["SPY", "QQQ", "IWM", "DIA", "VOO", "VTI"], StringComparer.OrdinalIgnoreCase);

    private static DateOnly NextWeekday(DateOnly d) => d.DayOfWeek switch
    {
        DayOfWeek.Saturday => d.AddDays(2),
        DayOfWeek.Sunday => d.AddDays(1),
        _ => d,
    };

    private DateOnly Today() => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    /// <summary>FNV-1a; string.GetHashCode is randomised per process and would break determinism.</summary>
    private static int Seed(params string[] parts)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in string.Join('|', parts))
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
