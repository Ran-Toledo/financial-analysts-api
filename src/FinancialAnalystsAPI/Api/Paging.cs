using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace FinancialAnalystsAPI.Api;

/// <summary>Common query-string parameters for list endpoints.</summary>
public class PagedQuery
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = 25;

    /// <summary>Sort key; each endpoint documents its allowed values.</summary>
    [StringLength(30)]
    public string? Sort { get; set; }

    [RegularExpression("^(?i)(asc|desc)$", ErrorMessage = "dir must be 'asc' or 'desc'.")]
    public string? Dir { get; set; }

    public bool Descending => string.Equals(Dir, "desc", StringComparison.OrdinalIgnoreCase);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, PagedQuery paging, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize).ToListAsync(ct);
        return new PagedResult<T>(items, paging.Page, paging.PageSize, total);
    }
}

/// <summary>
/// Whitelist of sort keys for one entity. Sorting is never built from raw strings, and a tiebreaker
/// keeps paging stable when sort values repeat.
/// </summary>
public sealed class SortOptions<T>
{
    private readonly Dictionary<string, Func<IQueryable<T>, bool, IOrderedQueryable<T>>> _sorts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<IOrderedQueryable<T>, IOrderedQueryable<T>> _tiebreak;

    public SortOptions(string defaultKey, bool defaultDescending, Expression<Func<T, int>> tiebreak)
    {
        DefaultKey = defaultKey;
        DefaultDescending = defaultDescending;
        _tiebreak = q => q.ThenBy(tiebreak);
    }

    public string DefaultKey { get; }
    public bool DefaultDescending { get; }
    public IEnumerable<string> Keys => _sorts.Keys;

    public SortOptions<T> Add<TKey>(string key, Expression<Func<T, TKey>> selector)
    {
        _sorts[key] = (q, desc) => desc ? q.OrderByDescending(selector) : q.OrderBy(selector);
        return this;
    }

    /// <summary>Applies <paramref name="paging"/>'s sort, or the default when none is given. False for unknown keys.</summary>
    public bool TryApply(IQueryable<T> query, PagedQuery paging, out IQueryable<T> sorted)
    {
        var key = string.IsNullOrWhiteSpace(paging.Sort) ? DefaultKey : paging.Sort;
        var desc = paging.Dir is null && string.IsNullOrWhiteSpace(paging.Sort) ? DefaultDescending : paging.Descending;

        if (!_sorts.TryGetValue(key, out var apply))
        {
            sorted = query;
            return false;
        }

        sorted = _tiebreak(apply(query, desc));
        return true;
    }
}
