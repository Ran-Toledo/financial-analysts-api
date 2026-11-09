using FinancialAnalystsAPI.Api;
using FinancialAnalystsAPI.Scoring;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FinancialAnalystsAPI.Controllers;

/// <summary>
/// Who calls it right. Scores come from matured calls: a Buy is right if the stock beat the benchmark over the horizon,
/// a Sell if it lagged, a Hold if it stayed within the hold band. See the README for the full method.
/// </summary>
[Route("api/leaderboards")]
[OutputCache(PolicyName = CachePolicies.MarketData)]
public class LeaderboardsController(ScoreQueries scores) : ApiControllerBase
{
    private static readonly string[] SortKeys = ["score", "hitRate", "avgExcessReturn", "calls", "name"];

    /// <summary>Research firms ranked by score. Sort: score (default), hitRate, avgExcessReturn, calls, name.</summary>
    [HttpGet("firms")]
    public Task<ActionResult<LeaderboardDto>> Firms([FromQuery] LeaderboardQuery q, CancellationToken ct) =>
        GetAsync(ScoreSubject.Firm, q, ct);

    /// <summary>
    /// Individual analysts ranked by score; filter by <c>firmId</c>. Empty until the data provider supplies analyst
    /// names (FMP's free tier names firms only).
    /// </summary>
    [HttpGet("analysts")]
    public Task<ActionResult<LeaderboardDto>> Analysts([FromQuery] LeaderboardQuery q, CancellationToken ct) =>
        GetAsync(ScoreSubject.Analyst, q, ct);

    private async Task<ActionResult<LeaderboardDto>> GetAsync(ScoreSubject subject, LeaderboardQuery q, CancellationToken ct)
    {
        if (!TryResolveHorizon(q.Horizon, scores.Options, "horizon", out var horizon, out var error)) return error!;

        var sort = q.Sort ?? "score";
        if (!SortKeys.Contains(sort, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("sort", $"Unknown sort '{q.Sort}'. Allowed: {string.Join(", ", SortKeys)}.");
            return ValidationProblem(ModelState);
        }

        // Default direction is "best first" for every key except name.
        var descending = q.Dir is null ? !sort.Equals("name", StringComparison.OrdinalIgnoreCase) : q.Descending;

        return await scores.LeaderboardAsync(subject, horizon, q.Sector, q.FirmId, q.MinCalls ?? scores.Options.MinCallsToRank,
            sort, descending, q.Page, q.PageSize, q.TrendMonths, ct);
    }
}
