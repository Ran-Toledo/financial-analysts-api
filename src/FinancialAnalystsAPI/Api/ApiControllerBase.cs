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
}
