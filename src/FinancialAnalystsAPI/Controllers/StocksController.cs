using System.ComponentModel.DataAnnotations;
using FinancialAnalystsAPI.Api;
using FinancialAnalystsAPI.Consensus;
using FinancialAnalystsAPI.Contracts;
using FinancialAnalystsAPI.Data;
using FinancialAnalystsAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace FinancialAnalystsAPI.Controllers;

[Route("api/stocks")]
[OutputCache(PolicyName = CachePolicies.MarketData)]
public class StocksController(AnalystsDbContext db, ConsensusService consensus) : ApiControllerBase
{
    private static readonly SortOptions<Stock> Sorts = new SortOptions<Stock>("ticker", defaultDescending: false, s => s.Id)
        .Add("ticker", s => s.Ticker)
        .Add("name", s => s.Name)
        .Add("sector", s => s.Sector)
        .Add("ratingCount", s => s.Ratings.Count);

    /// <summary>Stocks in the tracked universe. Sort: ticker (default), name, sector, ratingCount.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<StockDto>>> List([FromQuery] StockListQuery q, CancellationToken ct)
    {
        var stocks = db.Stocks.AsNoTracking();
        if (!q.IncludeUntracked) stocks = stocks.Where(s => s.IsTracked);
        if (q.Sector is { } sector) stocks = stocks.Where(s => s.Sector != null && s.Sector.ToLower() == sector.ToLower());
        if (q.Search is { } search)
        {
            var term = search.Trim().ToLower();
            stocks = stocks.Where(s => s.Ticker.ToLower().Contains(term) || s.Name.ToLower().Contains(term));
        }

        if (!Sorts.TryApply(stocks, q, out var sorted)) return InvalidSort(Sorts, q.Sort);
        return await sorted.Select(Projections.Stock).ToPagedAsync(q, ct);
    }

    /// <summary>Distinct sectors in the tracked universe, for filter menus.</summary>
    [HttpGet("sectors")]
    public async Task<ActionResult<IReadOnlyList<string>>> Sectors(CancellationToken ct) =>
        await db.Stocks.Where(s => s.IsTracked && s.Sector != null)
            .Select(s => s.Sector!).Distinct().OrderBy(s => s).ToListAsync(ct);

    [HttpGet("{ticker}")]
    public async Task<ActionResult<StockDto>> Get([FromRoute, RegularExpression(TickerPattern)] string ticker, CancellationToken ct)
    {
        var symbol = ticker.ToUpperInvariant();
        var stock = await db.Stocks.AsNoTracking().Where(s => s.Ticker == symbol).Select(Projections.Stock).SingleOrDefaultAsync(ct);
        return stock is null ? StockNotFound(symbol) : stock;
    }

    /// <summary>Ratings on this stock, newest first. Sort: date (default), firm, action.</summary>
    [HttpGet("{ticker}/ratings")]
    public async Task<ActionResult<PagedResult<RatingDto>>> Ratings(
        [FromRoute, RegularExpression(TickerPattern)] string ticker, [FromQuery] RatingListQuery q, CancellationToken ct)
    {
        var symbol = ticker.ToUpperInvariant();
        var stockId = await StockIdAsync(symbol, ct);
        if (stockId is null) return StockNotFound(symbol);

        var ratings = db.Ratings.AsNoTracking().Where(r => r.StockId == stockId).Filter(q);
        if (!RatingQueries.Sorts.TryApply(ratings, q, out var sorted)) return InvalidSort(RatingQueries.Sorts, q.Sort);
        return await sorted.Select(Projections.Rating).ToPagedAsync(q, ct);
    }

    /// <summary>Daily closes, oldest first. Defaults to the last year; at most 10 years per request.</summary>
    [HttpGet("{ticker}/prices")]
    public async Task<ActionResult<IReadOnlyList<PriceDto>>> Prices(
        [FromRoute, RegularExpression(TickerPattern)] string ticker, [FromQuery] PriceQuery q, CancellationToken ct)
    {
        var symbol = ticker.ToUpperInvariant();
        var stockId = await StockIdAsync(symbol, ct);
        if (stockId is null) return StockNotFound(symbol);

        var to = q.To ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = q.From ?? to.AddYears(-1);
        if (to.DayNumber - from.DayNumber > PriceQuery.MaxDays) from = to.AddDays(-PriceQuery.MaxDays);

        return await db.PriceSnapshots.AsNoTracking()
            .Where(p => p.StockId == stockId && p.Date >= from && p.Date <= to)
            .OrderBy(p => p.Date)
            .Select(p => new PriceDto(p.Date, p.Close, p.AdjClose))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Buy/Hold/Sell split and price-target summary from each contributor's latest call in the lookback window.
    /// <c>targets</c> is null when no call in the window has a price target.
    /// </summary>
    [HttpGet("{ticker}/consensus")]
    public async Task<ActionResult<ConsensusDto>> Consensus(
        [FromRoute, RegularExpression(TickerPattern)] string ticker, [FromQuery] ConsensusQuery q, CancellationToken ct)
    {
        var symbol = ticker.ToUpperInvariant();
        var result = await consensus.GetAsync(symbol, q.AsOf, q.LookbackDays, ct);
        return result is null ? StockNotFound(symbol) : result;
    }

    /// <summary>Consensus at each month-end over the last <c>months</c> months, ending today.</summary>
    [HttpGet("{ticker}/consensus/history")]
    public async Task<ActionResult<ConsensusHistoryDto>> ConsensusHistory(
        [FromRoute, RegularExpression(TickerPattern)] string ticker, [FromQuery] ConsensusHistoryQuery q, CancellationToken ct)
    {
        var symbol = ticker.ToUpperInvariant();
        var result = await consensus.GetHistoryAsync(symbol, q.Months, q.LookbackDays, ct);
        return result is null ? StockNotFound(symbol) : result;
    }

    private Task<int?> StockIdAsync(string ticker, CancellationToken ct) =>
        db.Stocks.Where(s => s.Ticker == ticker).Select(s => (int?)s.Id).SingleOrDefaultAsync(ct);

    private ActionResult StockNotFound(string ticker) => NotFoundProblem($"No stock with ticker '{ticker}'.");
}
