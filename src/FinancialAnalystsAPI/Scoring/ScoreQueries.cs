using FinancialAnalystsAPI.Consensus;
using FinancialAnalystsAPI.Data;
using FinancialAnalystsAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinancialAnalystsAPI.Scoring;

public enum ScoreSubject
{
    Firm,
    Analyst,
}

/// <param name="Rank">Position by score among subjects with at least the minimum calls.</param>
/// <param name="Trend">Score at each month-end, oldest first; only months where the subject had matured calls.</param>
public sealed record LeaderboardEntryDto(
    int Rank, int Id, string Name, int? FirmId, string? FirmName, ScoreStats Stats, IReadOnlyList<TrendPoint>? Trend);

public sealed record LeaderboardDto(
    ScoreSubject Subject,
    int HorizonMonths,
    string? Sector,
    int MinCalls,
    IReadOnlyList<LeaderboardEntryDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

/// <param name="Rank">Null when the subject has fewer than the minimum calls at this horizon.</param>
public sealed record HorizonScoreDto(int HorizonMonths, ScoreStats? Stats, int? Rank, int RankedCount);

public sealed record ScoreCardDto(
    ScoreSubject Subject,
    int Id,
    string Name,
    int? FirmId,
    string? FirmName,
    int MinCallsToRank,
    IReadOnlyList<HorizonScoreDto> Horizons,
    int TrendHorizonMonths,
    IReadOnlyList<TrendPoint> Trend);

public sealed record CallOutcomeDto(
    int RatingId,
    DateOnly CallDate,
    DateOnly EndDate,
    int HorizonMonths,
    string Ticker,
    string StockName,
    string? Sector,
    int FirmId,
    string FirmName,
    int? AnalystId,
    string? AnalystName,
    RatingAction Action,
    RatingCategory Category,
    double StartPrice,
    double EndPrice,
    double Return,
    double BenchmarkReturn,
    double ExcessReturn,
    bool Hit,
    double? Target,
    bool? TargetHit);

/// <summary>Read side of scoring: aggregates stored outcomes in memory (thousands of rows) per request.</summary>
public sealed class ScoreQueries(AnalystsDbContext db, IOptions<ScoringOptions> options, TimeProvider time)
{
    public ScoringOptions Options => options.Value;

    public async Task<LeaderboardDto> LeaderboardAsync(
        ScoreSubject subject, int horizon, string? sector, int? firmId, int minCalls,
        string sort, bool descending, int page, int pageSize, int trendMonths, CancellationToken ct)
    {
        var rows = await LoadAsync(horizon, sector, ct);
        if (subject == ScoreSubject.Analyst && firmId is { } f) rows = rows.Where(r => r.FirmId == f).ToList();

        var key = SubjectKey(subject);
        var ranked = Rank(ScoreAggregator.Aggregate(rows, key, Options), minCalls);
        var names = await NamesAsync(subject, ranked.Select(r => r.Id), ct);

        var sorted = (sort.ToLowerInvariant(), descending) switch
        {
            ("hitrate", var d) => Order(ranked, x => x.Stats.HitRate, d),
            ("avgexcessreturn", var d) => Order(ranked, x => x.Stats.AvgExcessReturn, d),
            ("calls", var d) => Order(ranked, x => x.Stats.Calls, d),
            ("name", var d) => Order(ranked, x => names[x.Id].Name, d),
            (_, var d) => Order(ranked, x => -x.Rank, d), // "score": rank order
        };

        var pageItems = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        Dictionary<int, List<TrendPoint>> trends = [];
        if (trendMonths > 0 && pageItems.Count > 0)
            trends = ScoreAggregator.Trend(rows, key, ConsensusService.MonthEnds(Today(), trendMonths), Options);

        var items = pageItems.Select(x =>
        {
            var n = names[x.Id];
            return new LeaderboardEntryDto(x.Rank, x.Id, n.Name, n.FirmId, n.FirmName, x.Stats,
                trendMonths > 0 ? trends.GetValueOrDefault(x.Id) ?? [] : null);
        }).ToList();

        var totalPages = ranked.Count == 0 ? 0 : (int)Math.Ceiling(ranked.Count / (double)pageSize);
        return new LeaderboardDto(subject, horizon, sector, minCalls, items, page, pageSize, ranked.Count, totalPages);
    }

    /// <returns>Null when the firm or analyst doesn't exist.</returns>
    public async Task<ScoreCardDto?> ScoreCardAsync(ScoreSubject subject, int id, int trendHorizon, int trendMonths, CancellationToken ct)
    {
        var names = await NamesAsync(subject, [id], ct);
        if (!names.TryGetValue(id, out var name)) return null;

        var key = SubjectKey(subject);
        var horizons = new List<HorizonScoreDto>();
        IReadOnlyList<TrendPoint> trend = [];

        foreach (var horizon in Options.Horizons)
        {
            var rows = await LoadAsync(horizon, sector: null, ct);
            var all = ScoreAggregator.Aggregate(rows, key, Options);
            var ranked = Rank(all, Options.MinCallsToRank);
            horizons.Add(new HorizonScoreDto(horizon, all.GetValueOrDefault(id),
                ranked.FirstOrDefault(r => r.Id == id)?.Rank, ranked.Count));

            if (horizon == trendHorizon && trendMonths > 0)
                trend = ScoreAggregator.Trend(rows, key, ConsensusService.MonthEnds(Today(), trendMonths), Options)
                    .GetValueOrDefault(id) ?? [];
        }

        return new ScoreCardDto(subject, id, name.Name, name.FirmId, name.FirmName, Options.MinCallsToRank, horizons, trendHorizon, trend);
    }

    /// <summary>Matured outcomes for a firm's or analyst's calls, as a query to page.</summary>
    public IQueryable<CallOutcome> Outcomes(ScoreSubject subject, int id, int horizon)
    {
        var outcomes = db.CallOutcomes.AsNoTracking().Where(o => o.HorizonMonths == horizon);
        return subject == ScoreSubject.Firm
            ? outcomes.Where(o => o.Rating.FirmId == id)
            : outcomes.Where(o => o.Rating.AnalystId == id);
    }

    public static readonly System.Linq.Expressions.Expression<Func<CallOutcome, CallOutcomeDto>> OutcomeProjection = o =>
        new CallOutcomeDto(
            o.RatingId, o.Rating.Date, o.EndDate, o.HorizonMonths,
            o.Rating.Stock.Ticker, o.Rating.Stock.Name, o.Rating.Stock.Sector,
            o.Rating.FirmId, o.Rating.Firm.Name, o.Rating.AnalystId, o.Rating.Analyst != null ? o.Rating.Analyst.Name : null,
            o.Rating.Action, o.Category, o.StartPrice, o.EndPrice, o.Return, o.BenchmarkReturn, o.ExcessReturn,
            o.Hit, o.Target, o.TargetHit);

    private Task<List<OutcomeRow>> LoadAsync(int horizon, string? sector, CancellationToken ct)
    {
        var query = db.CallOutcomes.AsNoTracking().Where(o => o.HorizonMonths == horizon);
        if (sector is not null)
        {
            var s = sector.ToLower();
            query = query.Where(o => o.Rating.Stock.Sector != null && o.Rating.Stock.Sector.ToLower() == s);
        }

        return query.Select(o => new OutcomeRow(
                o.RatingId, o.Rating.FirmId, o.Rating.AnalystId, o.Rating.Stock.Sector, o.HorizonMonths,
                o.Rating.Date, o.EndDate, o.Category, o.Return, o.ExcessReturn, o.DirectionalExcess,
                o.Hit, o.TargetHit, o.TargetError))
            .ToListAsync(ct);
    }

    private sealed record Ranked(int Rank, int Id, ScoreStats Stats);

    /// <summary>Ranks subjects with at least <paramref name="minCalls"/> calls: score, then more calls, then id.</summary>
    private static List<Ranked> Rank(Dictionary<int, ScoreStats> stats, int minCalls) =>
        stats.Where(s => s.Value.Calls >= minCalls)
            .OrderByDescending(s => s.Value.Score).ThenByDescending(s => s.Value.Calls).ThenBy(s => s.Key)
            .Select((s, i) => new Ranked(i + 1, s.Key, s.Value))
            .ToList();

    private static IEnumerable<Ranked> Order<TKey>(IEnumerable<Ranked> items, Func<Ranked, TKey> key, bool descending) =>
        descending ? items.OrderByDescending(key).ThenBy(x => x.Rank) : items.OrderBy(key).ThenBy(x => x.Rank);

    private static Func<OutcomeRow, int?> SubjectKey(ScoreSubject subject) =>
        subject == ScoreSubject.Firm ? r => r.FirmId : r => r.AnalystId;

    private sealed record SubjectName(string Name, int? FirmId, string? FirmName);

    private async Task<Dictionary<int, SubjectName>> NamesAsync(ScoreSubject subject, IEnumerable<int> ids, CancellationToken ct)
    {
        var idList = ids.Distinct().ToArray();
        return subject == ScoreSubject.Firm
            ? await db.Firms.Where(f => idList.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => new SubjectName(f.Name, null, null), ct)
            : await db.Analysts.Where(a => idList.Contains(a.Id))
                .Select(a => new { a.Id, a.Name, a.FirmId, FirmName = a.Firm.Name })
                .ToDictionaryAsync(a => a.Id, a => new SubjectName(a.Name, a.FirmId, a.FirmName), ct);
    }

    private DateOnly Today() => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
}
