namespace FinancialAnalystsAPI.Contracts;

/// <param name="Rating">Legacy hand-entered rating; replaced by a computed score in Phase 3.</param>
public sealed record AnalystDto(int Id, string Name, int FirmId, string FirmName, double? Rating, int RatingCount);
