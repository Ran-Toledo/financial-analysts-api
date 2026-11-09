namespace FinancialAnalystsAPI.Models;

public enum RatingAction
{
    Initiate,
    Upgrade,
    Downgrade,
    Maintain,
    Reiterate,
    TargetRaise,
    TargetLower,
}

public enum RatingCategory
{
    Buy,
    Hold,
    Sell,
}

public enum IngestionStatus
{
    Running,
    Succeeded,
    PartiallySucceeded,
    Failed,
}
