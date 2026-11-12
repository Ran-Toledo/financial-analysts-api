using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FinancialAnalystsAPI.Api;

public static class CachePolicies
{
    /// <summary>Policy for read endpoints over ingested data. Evicted by tag after each ingestion run.</summary>
    public const string MarketData = "market-data";
    public const string MarketDataTag = "market-data";
}

public sealed class ApiLimitsOptions
{
    public const string Section = "ApiLimits";

    /// <summary>Requests per minute per IP address without an API key.</summary>
    [Range(1, 1_000_000)]
    public int AnonymousPerMinute { get; set; } = 60;

    /// <summary>Requests per minute per API key.</summary>
    [Range(1, 1_000_000)]
    public int ApiKeyPerMinute { get; set; } = 600;

    /// <summary>How long read responses stay cached. Ingestion also clears the cache when it finishes.</summary>
    [Range(1, 1440)]
    public int CacheMinutes { get; set; } = 60;
}

public static class ApiLimitsServiceCollectionExtensions
{
    public static IServiceCollection AddApiCachingAndRateLimits(this IServiceCollection services)
    {
        services.AddOptions<ApiLimitsOptions>()
            .BindConfiguration(ApiLimitsOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOutputCache();
        services.AddOptions<Microsoft.AspNetCore.OutputCaching.OutputCacheOptions>()
            .Configure<IOptions<ApiLimitsOptions>>((cache, limits) =>
                cache.AddPolicy(CachePolicies.MarketData, policy => policy
                    .Expire(TimeSpan.FromMinutes(limits.Value.CacheMinutes))
                    .Tag(CachePolicies.MarketDataTag)
                    // With locking, concurrent requests for one URL wait on the first; if that client disconnects,
                    // its cancellation fails all of them with a 500. Reads are cheap, so let each run on its own.
                    .SetLocking(false)));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(Partition);
            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

                await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too Many Requests",
                        Detail = "Rate limit exceeded. Retry after the number of seconds in the Retry-After header, or use an API key for a higher limit.",
                    },
                });
            };
        });

        return services;
    }

    /// <summary>One fixed one-minute window per API client, or per IP address for anonymous callers. Only /api is limited.</summary>
    private static RateLimitPartition<string> Partition(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
            return RateLimitPartition.GetNoLimiter("unlimited");

        var limits = context.RequestServices.GetRequiredService<IOptions<ApiLimitsOptions>>().Value;
        var authenticated = context.User.Identity is { IsAuthenticated: true, Name: { } name };

        var (key, permits) = authenticated
            ? ($"key:{context.User.Identity!.Name}", limits.ApiKeyPerMinute)
            : ($"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}", limits.AnonymousPerMinute);

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    }
}
