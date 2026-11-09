using FinancialAnalystsAPI.Models;

namespace FinancialAnalystsAPI.Ingestion;

/// <summary>Maps firm-specific rating vocabulary and provider action strings onto the API's enums.</summary>
public static class RatingNormalizer
{
    private static readonly Dictionary<string, RatingCategory> Categories = Build(
        (RatingCategory.Buy,
        [
            "buy", "strong buy", "conviction buy", "moderate buy", "speculative buy", "long term buy", "top pick",
            "outperform", "market outperform", "sector outperform", "outperformer", "overweight", "accumulate",
            "add", "positive",
        ]),
        (RatingCategory.Hold,
        [
            "hold", "neutral", "market perform", "sector perform", "peer perform", "perform", "equal weight",
            "sector weight", "market weight", "in line", "mixed", "fair value", "average",
        ]),
        (RatingCategory.Sell,
        [
            "sell", "strong sell", "moderate sell", "underperform", "market underperform", "sector underperform",
            "underweight", "reduce", "negative",
        ]));

    /// <summary>Returns null for unknown strings so they are left out of consensus rather than guessed.</summary>
    public static RatingCategory? ToCategory(string? rating) =>
        rating is not null && Categories.TryGetValue(Key(rating), out var category) ? category : null;

    /// <summary>
    /// Upgrades, downgrades and initiations keep their action. Otherwise a moved price target becomes
    /// TargetRaise/TargetLower, and anything else is Reiterate or Maintain.
    /// </summary>
    public static RatingAction ToAction(string? rawAction, double? previousTarget, double? newTarget)
    {
        var action = Key(rawAction ?? string.Empty) switch
        {
            "upgrade" or "upgraded" => RatingAction.Upgrade,
            "downgrade" or "downgraded" => RatingAction.Downgrade,
            "init" or "initiate" or "initiated" or "initialise" or "initialize" => RatingAction.Initiate,
            "reiterate" or "reiterated" => RatingAction.Reiterate,
            _ => RatingAction.Maintain,
        };

        if (action is RatingAction.Upgrade or RatingAction.Downgrade or RatingAction.Initiate) return action;

        if (previousTarget is { } prev && newTarget is { } next && Math.Abs(next - prev) > 0.005)
            return next > prev ? RatingAction.TargetRaise : RatingAction.TargetLower;

        return action;
    }

    private static string Key(string value) =>
        string.Join(' ', value.ToLowerInvariant().Replace('-', ' ').Replace('_', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static Dictionary<string, RatingCategory> Build(params (RatingCategory Category, string[] Terms)[] groups) =>
        groups.SelectMany(g => g.Terms.Select(t => (Key: Key(t), g.Category)))
            .ToDictionary(x => x.Key, x => x.Category);
}
