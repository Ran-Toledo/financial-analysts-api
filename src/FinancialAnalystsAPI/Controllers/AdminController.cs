using FinancialAnalystsAPI.Api;
using FinancialAnalystsAPI.Auth;
using FinancialAnalystsAPI.Contracts;
using FinancialAnalystsAPI.Data;
using FinancialAnalystsAPI.Ingestion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinancialAnalystsAPI.Controllers;

/// <summary>Operational endpoints. Require an API key with the Admin role.</summary>
[Route("api/admin")]
[Authorize(Policy = ApiKeys.AdminPolicy)]
public class AdminController(AnalystsDbContext db, IngestionCoordinator coordinator) : ApiControllerBase
{
    /// <summary>Ingestion runs, newest first.</summary>
    [HttpGet("ingestion/runs")]
    public async Task<ActionResult<PagedResult<IngestionRunDto>>> Runs([FromQuery] PagedQuery q, CancellationToken ct) =>
        await db.IngestionRuns.AsNoTracking()
            .OrderByDescending(r => r.Id)
            .Select(Projections.IngestionRun)
            .ToPagedAsync(q, ct);

    [HttpGet("ingestion/runs/{id:int}")]
    public async Task<ActionResult<IngestionRunDto>> Run(int id, CancellationToken ct)
    {
        var run = await db.IngestionRuns.AsNoTracking().Where(r => r.Id == id).Select(Projections.IngestionRun).SingleOrDefaultAsync(ct);
        return run is null ? NotFoundProblem($"No ingestion run with id {id}.") : run;
    }

    /// <summary>
    /// Starts an ingestion run in the background and returns 202 at once; follow it at <c>/api/admin/ingestion/runs</c>.
    /// 409 if a run is already in progress. A run spends provider API calls (about 3–5 per ticker on FMP).
    /// </summary>
    [HttpPost("ingestion/run")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult StartRun([FromQuery] bool backfill = false)
    {
        if (!coordinator.TryStartInBackground(backfill))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: "An ingestion run is already in progress.");

        return Accepted("/api/admin/ingestion/runs", new { message = "Ingestion started.", backfill });
    }
}
