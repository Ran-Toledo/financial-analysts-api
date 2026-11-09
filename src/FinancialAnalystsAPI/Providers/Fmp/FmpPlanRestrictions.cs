using System.Collections.Concurrent;

namespace FinancialAnalystsAPI.Providers.Fmp;

/// <summary>
/// Remembers endpoints the FMP plan has refused (402), so a run doesn't spend one call per ticker
/// rediscovering it. Entries expire after a day, so an upgraded plan is picked up automatically.
/// </summary>
public sealed class FmpPlanRestrictions(TimeProvider time)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _restrictedUntil = new(StringComparer.Ordinal);

    public bool IsRestricted(string path) =>
        _restrictedUntil.TryGetValue(path, out var until) && until > time.GetUtcNow();

    /// <returns>True the first time the endpoint is marked (until it expires), so callers can log once.</returns>
    public bool MarkRestricted(string path)
    {
        var wasRestricted = IsRestricted(path);
        _restrictedUntil[path] = time.GetUtcNow() + Ttl;
        return !wasRestricted;
    }
}
