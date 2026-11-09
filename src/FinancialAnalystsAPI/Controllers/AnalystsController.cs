using FinancialAnalystsAPI.Api;
using FinancialAnalystsAPI.Contracts;
using FinancialAnalystsAPI.Data;
using FinancialAnalystsAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using FinancialAnalystsAPI.Scoring;
using Microsoft.EntityFrameworkCore;

namespace FinancialAnalystsAPI.Controllers;

/// <summary>
/// Read-only: analysts are created by ingestion. Empty on FMP's free tier, which names firms but not analysts.
/// </summary>
[Route("api/analysts")]
[OutputCache(PolicyName = CachePolicies.MarketData)]
public class AnalystsController(AnalystsDbContext db, ScoreQueries scores) : ScoredSubjectController(scores, ScoreSubject.Analyst)
{
    private static readonly SortOptions<Analyst> Sorts = new SortOptions<Analyst>("name", defaultDescending: false, a => a.Id)
        .Add("name", a => a.Name)
        .Add("firm", a => a.Firm.Name)
        .Add("ratingCount", a => a.Ratings.Count);

    /// <summary>Sort: name (default), firm, ratingCount.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AnalystDto>>> List([FromQuery] NameListQuery q, CancellationToken ct)
    {
        var analysts = db.Analysts.AsNoTracking();
        if (q.FirmId is { } firmId) analysts = analysts.Where(a => a.FirmId == firmId);
        if (q.Search is { } search)
        {
            var term = search.Trim().ToLower();
            analysts = analysts.Where(a => a.Name.ToLower().Contains(term));
        }

        if (!Sorts.TryApply(analysts, q, out var sorted)) return InvalidSort(Sorts, q.Sort);
        return await sorted.Select(Projections.Analyst).ToPagedAsync(q, ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AnalystDto>> Get(int id, CancellationToken ct)
    {
        var analyst = await db.Analysts.AsNoTracking().Where(a => a.Id == id).Select(Projections.Analyst).SingleOrDefaultAsync(ct);
        return analyst is null ? NotFoundProblem($"No analyst with id {id}.") : analyst;
    }

    /// <summary>The analyst's calls, newest first. Sort: date (default), ticker, action.</summary>
    [HttpGet("{id:int}/ratings")]
    public async Task<ActionResult<PagedResult<RatingDto>>> Ratings(int id, [FromQuery] RatingListQuery q, CancellationToken ct)
    {
        if (!await db.Analysts.AnyAsync(a => a.Id == id, ct)) return NotFoundProblem($"No analyst with id {id}.");

        var ratings = db.Ratings.AsNoTracking().Where(r => r.AnalystId == id).Filter(q);
        if (!RatingQueries.Sorts.TryApply(ratings, q, out var sorted)) return InvalidSort(RatingQueries.Sorts, q.Sort);
        return await sorted.Select(Projections.Rating).ToPagedAsync(q, ct);
    }

    protected override Task<bool> ExistsAsync(int id, CancellationToken ct) => db.Analysts.AnyAsync(x => x.Id == id, ct);
}
