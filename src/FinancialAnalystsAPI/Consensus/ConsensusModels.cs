using FinancialAnalystsAPI.Models;

namespace FinancialAnalystsAPI.Consensus;

/// <param name="Score">Mean of Buy = 1, Hold = 2, Sell = 3 over contributors; null when there are none.</param>
/// <param name="Rating">Buy when Score ≤ 1.5, Sell when Score ≥ 2.5, otherwise Hold.</param>
/// <param name="Contributors">Analysts (or firms, when the provider names no analyst) with a call in the window.</param>
/// <param name="Targets">Null when no contributor has a price target in the window (e.g. FMP's free tier).</param>
public sealed record ConsensusDto(
    string Ticker,
    DateOnly AsOf,
    int LookbackDays,
    int Contributors,
    int Buy,
    int Hold,
    int Sell,
    double? Score,
    RatingCategory? Rating,
    TargetSummaryDto? Targets,
    double? LastClose,
    DateOnly? LastCloseDate);

/// <param name="UpsidePct">Mean target versus the last close, in percent.</param>
public sealed record TargetSummaryDto(int Count, double Mean, double Median, double High, double Low, double? UpsidePct);

public sealed record ConsensusPointDto(
    DateOnly AsOf,
    int Contributors,
    int Buy,
    int Hold,
    int Sell,
    double? Score,
    RatingCategory? Rating,
    double? MeanTarget,
    double? Close);

public sealed record ConsensusHistoryDto(string Ticker, int LookbackDays, IReadOnlyList<ConsensusPointDto> Points);

/// <summary>The fields of a rating that consensus needs.</summary>
public readonly record struct ConsensusCall(DateOnly Date, int FirmId, int? AnalystId, RatingCategory? Category, double? Target);

public readonly record struct ConsensusSnapshot(
    int Contributors, int Buy, int Hold, int Sell, double? Score, RatingCategory? Rating, IReadOnlyList<double> Targets);
