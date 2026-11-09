namespace FinancialAnalystsAPI.Contracts;

/// <summary>An analyst. Their computed score is at <c>/api/analysts/{id}/scores</c>.</summary>
public sealed record AnalystDto(int Id, string Name, int FirmId, string FirmName, int RatingCount);
