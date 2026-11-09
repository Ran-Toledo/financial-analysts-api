using FinancialAnalystsAPI.Api;
using FinancialAnalystsAPI.Contracts;
using FinancialAnalystsAPI.Data;
using FinancialAnalystsAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace FinancialAnalystsAPI.Controllers;

[Route("api/firms")]
[OutputCache(PolicyName = CachePolicies.MarketData)]
public class FirmsController(AnalystsDbContext db) : ApiControllerBase
{
    private static readonly SortOptions<Firm> Sorts = new SortOptions<Firm>("name", defaultDescending: false, f => f.Id)
        .Add("name", f => f.Name)
        .Add("ratingCount", f => f.Ratings.Count)
        .Add("lastRating", f => f.Ratings.Max(r => (DateOnly?)r.Date));

    /// <summary>Research firms. Sort: name (default), ratingCount, lastRating.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<FirmDto>>> List([FromQuery] NameListQuery q, CancellationToken ct)
    {
        var firms = db.Firms.AsNoTracking();
        if (q.Search is { } search)
        {
            var term = search.Trim().ToLower();
            firms = firms.Where(f => f.Name.ToLower().Contains(term));
        }

        if (!Sorts.TryApply(firms, q, out var sorted)) return InvalidSort(Sorts, q.Sort);
        return await sorted.Select(Projections.Firm).ToPagedAsync(q, ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FirmDto>> Get(int id, CancellationToken ct)
    {
        var firm = await db.Firms.AsNoTracking().Where(f => f.Id == id).Select(Projections.Firm).SingleOrDefaultAsync(ct);
        return firm is null ? NotFoundProblem($"No firm with id {id}.") : firm;
    }

    /// <summary>The firm's calls, newest first. Sort: date (default), ticker, action.</summary>
    [HttpGet("{id:int}/ratings")]
    public async Task<ActionResult<PagedResult<RatingDto>>> Ratings(int id, [FromQuery] RatingListQuery q, CancellationToken ct)
    {
        if (!await db.Firms.AnyAsync(f => f.Id == id, ct)) return NotFoundProblem($"No firm with id {id}.");

        var ratings = db.Ratings.AsNoTracking().Where(r => r.FirmId == id).Filter(q);
        if (!RatingQueries.Sorts.TryApply(ratings, q, out var sorted)) return InvalidSort(RatingQueries.Sorts, q.Sort);
        return await sorted.Select(Projections.Rating).ToPagedAsync(q, ct);
    }
}
