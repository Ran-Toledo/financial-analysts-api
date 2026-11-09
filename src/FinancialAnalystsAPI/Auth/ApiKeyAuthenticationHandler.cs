using System.Security.Claims;
using System.Text.Encodings.Web;
using FinancialAnalystsAPI.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FinancialAnalystsAPI.Auth;

/// <summary>
/// Authenticates the <c>X-Api-Key</c> header against hashed keys in <c>ApiClients</c>.
/// No header means anonymous, which read endpoints allow. A key that doesn't match is rejected with 401
/// everywhere (see <see cref="RejectInvalidKeysMiddleware"/>), so a typo never silently downgrades to anonymous.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    AnalystsDbContext db,
    IMemoryCache cache,
    IProblemDetailsService problemDetails) : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    internal const string InvalidKeyItem = "ApiKey.Invalid";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);

    private sealed record CachedClient(int Id, string Name, string Role);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeys.HeaderName, out var values) || string.IsNullOrWhiteSpace(values))
            return AuthenticateResult.NoResult();

        var hash = ApiKeys.Hash(values.ToString().Trim());

        // Revocations take effect within CacheFor.
        var client = await cache.GetOrCreateAsync($"apikey:{hash}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            return db.ApiClients.AsNoTracking()
                .Where(c => c.KeyHash == hash && c.IsActive)
                .Select(c => new CachedClient(c.Id, c.Name, c.Role.ToString()))
                .SingleOrDefaultAsync(Context.RequestAborted);
        });

        if (client is null)
        {
            Context.Items[InvalidKeyItem] = true;
            return AuthenticateResult.Fail("Invalid or revoked API key.");
        }

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, client.Id.ToString()),
            new(ClaimTypes.Name, client.Name),
            new(ClaimTypes.Role, client.Role),
        ];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = $"{ApiKeys.Scheme} header=\"{ApiKeys.HeaderName}\"";
        await WriteProblemAsync(StatusCodes.Status401Unauthorized, "Unauthorized",
            $"This endpoint requires an API key in the {ApiKeys.HeaderName} header.");
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        WriteProblemAsync(StatusCodes.Status403Forbidden, "Forbidden", "This API key doesn't have access to this endpoint.");

    private async Task WriteProblemAsync(int status, string title, string detail)
    {
        Response.StatusCode = status;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = Context,
            ProblemDetails = { Status = status, Title = title, Detail = detail },
        });
    }
}

/// <summary>Returns 401 when a request carries an API key that didn't authenticate, even on anonymous endpoints.</summary>
public sealed class RejectInvalidKeysMiddleware(RequestDelegate next, IProblemDetailsService problemDetails)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Items.ContainsKey(ApiKeyAuthenticationHandler.InvalidKeyItem))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails =
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = $"The {ApiKeys.HeaderName} header doesn't match an active API key.",
                },
            });
            return;
        }

        await next(context);
    }
}
