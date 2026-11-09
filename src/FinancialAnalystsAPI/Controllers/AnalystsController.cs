using FinancialAnalystsAPI.Contracts;
using FinancialAnalystsAPI.Data;
using FinancialAnalystsAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinancialAnalystsAPI.Controllers;

/// <summary>
/// Read-only: analysts are created by ingestion, so the old POST/PUT/DELETE endpoints are gone.
/// Paging, filtering and sorting arrive in Phase 2.
/// </summary>
[ApiController]
[Route("api/analysts")]
public class AnalystsController(AnalystsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AnalystDto>>> GetAnalysts(CancellationToken ct)
    {
        // Order before projecting: EF can't translate ORDER BY on a constructor-built DTO.
        return await Project(db.Analysts.AsNoTracking().OrderBy(a => a.Name))
            .ToListAsync(ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AnalystDto>> GetAnalyst(int id, CancellationToken ct)
    {
        var analyst = await Project(db.Analysts.AsNoTracking().Where(a => a.Id == id)).SingleOrDefaultAsync(ct);
        return analyst is null ? NotFound() : analyst;
    }

    private static IQueryable<AnalystDto> Project(IQueryable<Analyst> analysts) =>
        analysts.Select(a => new AnalystDto(a.Id, a.Name, a.FirmId, a.Firm.Name, a.ManualRating, a.Ratings.Count));
}
