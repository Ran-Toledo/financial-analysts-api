using Microsoft.AspNetCore.Mvc;

namespace FinancialAnalystsAPI.Api;

// No [Produces("application/json")]: it would also force error responses to application/json
// instead of application/problem+json.
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    public const string TickerPattern = @"^[A-Za-z][A-Za-z.\-]{0,9}$";

    /// <summary>400 ProblemDetails naming the allowed sort keys.</summary>
    protected ActionResult InvalidSort<T>(SortOptions<T> sorts, string? requested)
    {
        ModelState.AddModelError("sort", $"Unknown sort '{requested}'. Allowed: {string.Join(", ", sorts.Keys)}.");
        return ValidationProblem(ModelState);
    }

    protected ActionResult NotFoundProblem(string detail) =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found", detail: detail);

    /// <summary>400 unless <paramref name="requested"/> is a configured horizon; null means the default.</summary>
    protected bool TryResolveHorizon(int? requested, Scoring.ScoringOptions options, string field, out int horizon, out ActionResult? error)
    {
        horizon = requested ?? options.DefaultHorizonMonths;
        error = null;
        if (options.IsHorizon(horizon)) return true;

        ModelState.AddModelError(field, $"Unknown horizon {horizon}. Allowed: {string.Join(", ", options.Horizons)} (months).");
        error = ValidationProblem(ModelState);
        return false;
    }
}
