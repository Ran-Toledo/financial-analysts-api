using FinancialAnalystsAPI.Models;

namespace FinancialAnalystsAPI.Contracts;

public sealed record StockDto(
    int Id,
    string Ticker,
    string Name,
    string? Sector,
    string? Industry,
    string? Exchange,
    bool IsTracked,
    bool IsBenchmark,
    double? LastClose,
    DateOnly? LastCloseDate,
    int RatingCount);

public sealed record FirmDto(int Id, string Name, int RatingCount, int AnalystCount, int StocksCovered, DateOnly? LastRatingDate);

/// <summary>
/// One call. <see cref="AnalystId"/> is null when the provider names only the firm (FMP's free tier).
/// <see cref="PriceTargetTo"/> is null when the call carried no target.
/// </summary>
public sealed record RatingDto(
    int Id,
    DateOnly Date,
    string Ticker,
    string StockName,
    string? Sector,
    int FirmId,
    string FirmName,
    int? AnalystId,
    string? AnalystName,
    RatingAction Action,
    string? RatingFrom,
    string? RatingTo,
    RatingCategory? Category,
    bool RatingCarriedForward,
    double? PriceTargetFrom,
    double? PriceTargetTo,
    double? PriceAtCall);

public sealed record PriceDto(DateOnly Date, double Close, double? AdjClose);

public sealed record IngestionRunDto(
    int Id,
    string Provider,
    bool Backfill,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    IngestionStatus Status,
    int StocksProcessed,
    int PricesAdded,
    int RatingsAdded,
    int RatingsUpdated,
    string? Error);
