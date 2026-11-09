using FinancialAnalystsAPI.Api;
using FinancialAnalystsAPI.Contracts;
using FinancialAnalystsAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace FinancialAnalystsAPI.Controllers;

[Route("api/feed")]
[OutputCache(PolicyName = CachePolicies.MarketData)]
public class FeedController(AnalystsDbContext db) : ApiControllerBase
{
    /// <summary>
    /// Activity feed across tracked stocks, newest first. By default only rating changes (upgrades, downgrades,
    /// initiations); pass <c>includeAll=true</c> or explicit <c>action</c> values to widen it.
    /// Filters: ticker, sector, firmId, analystId, from, to, action, category. Sort: date (default), ticker, firm, action.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<RatingDto>>> Get([FromQuery] FeedQuery q, CancellationToken ct)
    {
        var ratings = db.Ratings.AsNoTracking().Where(r => r.Stock.IsTracked).Filter(q);

        if (q.Action is not { Length: > 0 } && !q.IncludeAll)
            ratings = ratings.Where(r => RatingQueries.RatingChanges.Contains(r.Action));

        if (q.Ticker is { } ticker)
        {
            var symbol = ticker.ToUpperInvariant();
            ratings = ratings.Where(r => r.Stock.Ticker == symbol);
        }
        if (q.Sector is { } sector) ratings = ratings.Where(r => r.Stock.Sector != null && r.Stock.Sector.ToLower() == sector.ToLower());
        if (q.FirmId is { } firmId) ratings = ratings.Where(r => r.FirmId == firmId);
        if (q.AnalystId is { } analystId) ratings = ratings.Where(r => r.AnalystId == analystId);

        if (!RatingQueries.Sorts.TryApply(ratings, q, out var sorted)) return InvalidSort(RatingQueries.Sorts, q.Sort);
        return await sorted.Select(Projections.Rating).ToPagedAsync(q, ct);
    }
}
