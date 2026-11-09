using FinancialAnalystsAPI.Models;
using FinancialAnalystsAPI.Scoring;
using Microsoft.AspNetCore.Mvc;

namespace FinancialAnalystsAPI.Api;

/// <summary>Shared scorecard and outcome actions for firms and analysts.</summary>
public abstract class ScoredSubjectController(ScoreQueries scores, ScoreSubject subject) : ApiControllerBase
{
    private static readonly SortOptions<CallOutcome> OutcomeSorts =
        new SortOptions<CallOutcome>("date", defaultDescending: true, o => o.RatingId)
            .Add("date", o => o.Rating.Date)
            .Add("ticker", o => o.Rating.Stock.Ticker)
            .Add("return", o => o.Return)
            .Add("excessReturn", o => o.ExcessReturn);

    protected abstract Task<bool> ExistsAsync(int id, CancellationToken ct);

    /// <summary>Score, hit rate, returns and rank at each horizon, plus a month-end score trend.</summary>
    [HttpGet("{id:int}/scores")]
    public async Task<ActionResult<ScoreCardDto>> Scores(int id, [FromQuery] ScoreCardQuery q, CancellationToken ct)
    {
        if (!TryResolveHorizon(q.TrendHorizon, scores.Options, "trendHorizon", out var horizon, out var error)) return error!;

        var card = await scores.ScoreCardAsync(subject, id, horizon, q.TrendMonths, ct);
        return card is null ? NotFoundProblem($"No {subject.ToString().ToLowerInvariant()} with id {id}.") : card;
    }

    /// <summary>How each matured call turned out. Sort: date (default), ticker, return, excessReturn.</summary>
    [HttpGet("{id:int}/outcomes")]
    public async Task<ActionResult<PagedResult<CallOutcomeDto>>> Outcomes(int id, [FromQuery] OutcomeQuery q, CancellationToken ct)
    {
        if (!TryResolveHorizon(q.Horizon, scores.Options, "horizon", out var horizon, out var error)) return error!;
        if (!await ExistsAsync(id, ct)) return NotFoundProblem($"No {subject.ToString().ToLowerInvariant()} with id {id}.");

        var outcomes = scores.Outcomes(subject, id, horizon);
        if (q.Hit is { } hit) outcomes = outcomes.Where(o => o.Hit == hit);

        if (!OutcomeSorts.TryApply(outcomes, q, out var sorted)) return InvalidSort(OutcomeSorts, q.Sort);
        return await sorted.Select(ScoreQueries.OutcomeProjection).ToPagedAsync(q, ct);
    }
}
