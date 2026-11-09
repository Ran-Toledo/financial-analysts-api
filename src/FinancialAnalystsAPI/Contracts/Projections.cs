using System.Linq.Expressions;
using FinancialAnalystsAPI.Models;

namespace FinancialAnalystsAPI.Contracts;

/// <summary>EF-translatable projections, so queries read only the columns each DTO needs.</summary>
public static class Projections
{
    public static readonly Expression<Func<Stock, StockDto>> Stock = s => new StockDto(
        s.Id, s.Ticker, s.Name, s.Sector, s.Industry, s.Exchange, s.IsTracked, s.IsBenchmark,
        s.Prices.OrderByDescending(p => p.Date).Select(p => (double?)p.Close).FirstOrDefault(),
        s.Prices.OrderByDescending(p => p.Date).Select(p => (DateOnly?)p.Date).FirstOrDefault(),
        s.Ratings.Count);

    public static readonly Expression<Func<Firm, FirmDto>> Firm = f => new FirmDto(
        f.Id, f.Name, f.Ratings.Count, f.Analysts.Count,
        f.Ratings.Select(r => r.StockId).Distinct().Count(),
        f.Ratings.OrderByDescending(r => r.Date).Select(r => (DateOnly?)r.Date).FirstOrDefault());

    public static readonly Expression<Func<Analyst, AnalystDto>> Analyst = a =>
        new AnalystDto(a.Id, a.Name, a.FirmId, a.Firm.Name, a.Ratings.Count);

    public static readonly Expression<Func<Rating, RatingDto>> Rating = r => new RatingDto(
        r.Id, r.Date, r.Stock.Ticker, r.Stock.Name, r.Stock.Sector,
        r.FirmId, r.Firm.Name, r.AnalystId, r.Analyst != null ? r.Analyst.Name : null,
        r.Action, r.RatingFrom, r.RatingTo, r.Category, r.RatingCarriedForward,
        r.PriceTargetFrom, r.PriceTargetTo, r.PriceAtCall);

    public static readonly Expression<Func<IngestionRun, IngestionRunDto>> IngestionRun = r => new IngestionRunDto(
        r.Id, r.Provider, r.Backfill, r.StartedAtUtc, r.FinishedAtUtc, r.Status,
        r.StocksProcessed, r.PricesAdded, r.RatingsAdded, r.RatingsUpdated, r.Error);
}
