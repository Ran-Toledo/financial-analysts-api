using FinancialAnalystsAPI.Models;

namespace FinancialAnalystsAPI.Api;

public static class RatingQueries
{
    /// <summary>Changes of opinion: what the activity feed shows unless <c>includeAll</c> is set.</summary>
    public static readonly RatingAction[] RatingChanges = [RatingAction.Upgrade, RatingAction.Downgrade, RatingAction.Initiate];

    public static readonly SortOptions<Rating> Sorts = new SortOptions<Rating>("date", defaultDescending: true, r => r.Id)
        .Add("date", r => r.Date)
        .Add("ticker", r => r.Stock.Ticker)
        .Add("firm", r => r.Firm.Name)
        .Add("action", r => r.Action);

    public static IQueryable<Rating> Filter(this IQueryable<Rating> ratings, RatingListQuery q)
    {
        if (q.From is { } from) ratings = ratings.Where(r => r.Date >= from);
        if (q.To is { } to) ratings = ratings.Where(r => r.Date <= to);
        if (q.Action is { Length: > 0 } actions) ratings = ratings.Where(r => actions.Contains(r.Action));
        if (q.Category is { } category) ratings = ratings.Where(r => r.Category == category);
        return ratings;
    }
}
