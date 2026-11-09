using System.Globalization;
using System.Net;
using System.Text.Json;
using FinancialAnalystsAPI.Ingestion;
using Microsoft.Extensions.Options;

namespace FinancialAnalystsAPI.Providers.Fmp;

/// <summary>
/// Financial Modeling Prep. FMP splits a call across two feeds: <c>grades</c> (firm, rating change) and
/// <c>price-target-news</c> (analyst, target). They are merged here into one <see cref="ProviderRating"/>
/// per firm per day.
/// </summary>
/// <remarks>
/// The API key travels as the <c>apikey</c> query parameter, so HttpClientFactory request logging is removed
/// for this client at registration (it would log full URLs) and this class never logs a URL.
/// <para>
/// If the plan doesn't include <c>price-target-news</c> (the free tier doesn't), ratings degrade to firm-level
/// grades with no analyst names or targets, and the endpoint is skipped for a day.
/// </para>
/// </remarks>
public sealed class FmpAnalystDataProvider(
    HttpClient http,
    IOptions<FmpOptions> options,
    FmpPlanRestrictions restrictions,
    ILogger<FmpAnalystDataProvider> logger) : IAnalystDataProvider
{
    public const string ProviderName = "fmp";
    internal const string PriceTargetPath = "price-target-news";

    public string Name => ProviderName;

    public async Task<ProviderStockProfile?> GetStockProfileAsync(string ticker, CancellationToken ct)
    {
        var profiles = await GetAsync<FmpProfile>("profile", ct, ("symbol", ticker));
        var p = profiles.FirstOrDefault();
        if (p is null || string.IsNullOrWhiteSpace(p.CompanyName)) return null;
        return new ProviderStockProfile(ticker, p.CompanyName.Trim(), Clean(p.Sector), Clean(p.Industry), Clean(p.Exchange));
    }

    public async Task<IReadOnlyList<ProviderPrice>> GetDailyPricesAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct)
    {
        (string, string)[] query = [("symbol", ticker), ("from", Iso(from)), ("to", Iso(to))];
        var closes = await GetAsync<FmpEodPrice>("historical-price-eod/full", ct, query);
        var adjusted = await GetAsync<FmpAdjustedPrice>("historical-price-eod/dividend-adjusted", ct, query);

        var adjByDate = adjusted
            .Where(a => a.AdjClose is not null && TryDate(a.Date, out _))
            .GroupBy(a => ParseDate(a.Date!))
            .ToDictionary(g => g.Key, g => g.First().AdjClose);

        return closes
            .Where(c => c.Close is > 0 && TryDate(c.Date, out _))
            .Select(c => (Date: ParseDate(c.Date!), Close: c.Close!.Value))
            .Where(c => c.Date >= from && c.Date <= to)
            .DistinctBy(c => c.Date)
            .OrderBy(c => c.Date)
            .Select(c => new ProviderPrice(c.Date, c.Close, adjByDate.GetValueOrDefault(c.Date)))
            .ToList();
    }

    public async Task<IReadOnlyList<ProviderRating>> GetRatingsAsync(string ticker, DateOnly since, CancellationToken ct)
    {
        var grades = (await GetAsync<FmpGrade>("grades", ct, ("symbol", ticker)))
            .Where(g => TryDate(g.Date, out var d) && d >= since && !string.IsNullOrWhiteSpace(g.GradingCompany))
            .ToList();

        List<FmpPriceTargetNews> targets = [];
        if (!restrictions.IsRestricted(PriceTargetPath))
        {
            try
            {
                targets = await GetPriceTargetsAsync(ticker, since, ct);
            }
            catch (ProviderRestrictedException ex) when (!ex.SymbolOnly)
            {
                if (restrictions.MarkRestricted(PriceTargetPath))
                {
                    logger.LogWarning(
                        "Your FMP plan doesn't include {Endpoint}, so ratings are firm-level grades only, with no analyst " +
                        "names or price targets. Skipping it for 24 hours. FMP said: {Message}", PriceTargetPath, ex.Message);
                }
            }
        }

        return Merge(ticker, grades, targets);
    }

    private async Task<List<FmpPriceTargetNews>> GetPriceTargetsAsync(string ticker, DateOnly since, CancellationToken ct)
    {
        var o = options.Value;
        var result = new List<FmpPriceTargetNews>();

        // Newest first; stop once a page reaches back past `since`, or at the page cap.
        for (var page = 0; page < o.MaxPriceTargetPages; page++)
        {
            var items = await GetAsync<FmpPriceTargetNews>(PriceTargetPath, ct,
                ("symbol", ticker), ("page", page.ToString(CultureInfo.InvariantCulture)),
                ("limit", o.PriceTargetPageSize.ToString(CultureInfo.InvariantCulture)));
            if (items.Count == 0) break;

            var dated = items.Where(t => TryDate(t.PublishedDate, out _)).ToList();
            result.AddRange(dated.Where(t => ParseDate(t.PublishedDate!) >= since));

            if (dated.Count == 0 || dated.Min(t => ParseDate(t.PublishedDate!)) < since || items.Count < o.PriceTargetPageSize)
                break;

            if (page == o.MaxPriceTargetPages - 1)
                logger.LogWarning("Stopped paging price targets for {Ticker} at the {Pages}-page cap; older targets were skipped",
                    ticker, o.MaxPriceTargetPages);
        }

        return result;
    }

    internal static IReadOnlyList<ProviderRating> Merge(string ticker, IEnumerable<FmpGrade> grades, IEnumerable<FmpPriceTargetNews> targets)
    {
        var gradeByKey = grades
            .GroupBy(g => (Date: ParseDate(g.Date!), Firm: NameNormalizer.Normalize(g.GradingCompany)))
            .Where(g => g.Key.Firm.Length > 0)
            .ToDictionary(g => g.Key, g => g.Last());

        var targetsByKey = targets
            .Where(t => !string.IsNullOrWhiteSpace(t.AnalystCompany))
            .GroupBy(t => (Date: ParseDate(t.PublishedDate!), Firm: NameNormalizer.Normalize(t.AnalystCompany)))
            .Where(g => g.Key.Firm.Length > 0)
            .ToDictionary(g => g.Key, g => g.ToList());

        var keys = gradeByKey.Keys.Union(targetsByKey.Keys).OrderBy(k => k.Date).ThenBy(k => k.Firm);
        var merged = new List<ProviderRating>();

        foreach (var key in keys)
        {
            gradeByKey.TryGetValue(key, out var grade);
            var dayTargets = targetsByKey.GetValueOrDefault(key) ?? [];

            // Several news items can report the same call; prefer one that names the analyst.
            var target = dayTargets.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.AnalystName)) ?? dayTargets.FirstOrDefault();

            merged.Add(new ProviderRating(
                Date: key.Date,
                Firm: (grade?.GradingCompany ?? target!.AnalystCompany!).Trim(),
                Analyst: Clean(target?.AnalystName),
                RatingFrom: Clean(grade?.PreviousGrade),
                RatingTo: Clean(grade?.NewGrade),
                Action: Clean(grade?.Action),
                PriceTarget: Positive(target?.AdjPriceTarget) ?? Positive(target?.PriceTarget),
                PriceWhenPosted: Positive(target?.PriceWhenPosted),
                SourceKey: $"{ticker.ToUpperInvariant()}:{Iso(key.Date)}:{key.Firm}"));
        }

        return merged;
    }

    private async Task<List<T>> GetAsync<T>(string path, CancellationToken ct, params (string Key, string Value)[] query)
    {
        var apiKey = options.Value.ApiKey
            ?? throw new InvalidOperationException("Fmp:ApiKey is not configured.");

        var qs = string.Join('&', query.Append(("apikey", apiKey))
            .Select(p => $"{Uri.EscapeDataString(p.Item1)}={Uri.EscapeDataString(p.Item2)}"));

        using var response = await http.GetAsync($"{path}?{qs}", ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        // Messages never include the request URI (it carries the key), and any echo of the key is masked.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new ProviderAuthenticationException(
                $"FMP rejected the API key ({(int)response.StatusCode}): {ErrorText(body, apiKey)} " +
                "Check Fmp:ApiKey: it should be the bare key, with no quotes or angle brackets.");
        }

        if (response.StatusCode == HttpStatusCode.PaymentRequired)
        {
            // "Restricted Endpoint" = not on this plan at all; "Premium Query Parameter" = not for this symbol.
            var text = ErrorText(body, apiKey);
            var symbolOnly = text.Contains("Premium Query Parameter", StringComparison.OrdinalIgnoreCase);
            throw new ProviderRestrictedException(
                symbolOnly
                    ? $"Your FMP plan doesn't cover this symbol on '{path}': {text}"
                    : $"Your FMP plan doesn't include '{path}': {text}",
                symbolOnly);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"FMP '{path}' returned {(int)response.StatusCode} {response.ReasonPhrase}: {ErrorText(body, apiKey)}",
                null, response.StatusCode);
        }

        // FMP returns an object instead of an array for errors such as "Limit Reach" or premium-only endpoints.
        if (body.TrimStart().StartsWith('{'))
            throw new HttpRequestException($"FMP '{path}' returned an error object: {ErrorText(body, apiKey)}");

        return JsonSerializer.Deserialize<List<T>>(body, JsonOptions) ?? [];
    }

    /// <summary>FMP's "Error Message" / "message" field when present, else the start of the body; key masked.</summary>
    internal static string ErrorText(string body, string apiKey)
    {
        var text = body.Trim();
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var name in (string[])["Error Message", "error", "message"])
                {
                    if (doc.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                    {
                        text = value.GetString()!;
                        break;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON: use the raw text.
        }

        if (text.Length == 0) text = "(empty response)";
        if (apiKey.Length > 0) text = text.Replace(apiKey, "***", StringComparison.Ordinal);
        return Truncate(text, 300);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static bool TryDate(string? value, out DateOnly date)
    {
        date = default;
        return value is { Length: >= 10 } &&
               DateOnly.TryParseExact(value[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static DateOnly ParseDate(string value) => DateOnly.ParseExact(value[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static double? Positive(double? v) => v is > 0 ? v : null;
    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
