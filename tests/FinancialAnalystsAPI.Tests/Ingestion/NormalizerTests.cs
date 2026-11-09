using FinancialAnalystsAPI.Ingestion;
using FinancialAnalystsAPI.Models;

namespace FinancialAnalystsAPI.Tests.Ingestion;

public class NormalizerTests
{
    [Theory]
    [InlineData("Morgan Stanley & Co.", "morgan stanley")]
    [InlineData("MORGAN STANLEY", "morgan stanley")]
    [InlineData("  J.P. Morgan  ", "j p morgan")]
    [InlineData("Société Générale", "societe generale")]
    [InlineData("Goldman Sachs Group, Inc.", "goldman sachs group")]
    [InlineData("Co", "co")] // a lone suffix is kept rather than emptied
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NameNormalizer_ProducesMatchKeys(string? input, string expected) =>
        Assert.Equal(expected, NameNormalizer.Normalize(input));

    [Theory]
    [InlineData("Overweight", RatingCategory.Buy)]
    [InlineData("Strong-Buy", RatingCategory.Buy)]
    [InlineData("Market Outperform", RatingCategory.Buy)]
    [InlineData("Equal-Weight", RatingCategory.Hold)]
    [InlineData("equal weight", RatingCategory.Hold)]
    [InlineData("In-Line", RatingCategory.Hold)]
    [InlineData("Sector Perform", RatingCategory.Hold)]
    [InlineData("Underweight", RatingCategory.Sell)]
    [InlineData("Reduce", RatingCategory.Sell)]
    public void ToCategory_MapsFirmVocabulary(string rating, RatingCategory expected) =>
        Assert.Equal(expected, RatingNormalizer.ToCategory(rating));

    [Theory]
    [InlineData("Not Rated")]
    [InlineData("")]
    [InlineData(null)]
    public void ToCategory_ReturnsNullForUnknown(string? rating) =>
        Assert.Null(RatingNormalizer.ToCategory(rating));

    [Theory]
    [InlineData("upgrade", 100.0, 90.0, RatingAction.Upgrade)]        // upgrade wins over a lowered target
    [InlineData("downgrade", null, null, RatingAction.Downgrade)]
    [InlineData("init", null, 120.0, RatingAction.Initiate)]
    [InlineData("maintain", 100.0, 110.0, RatingAction.TargetRaise)]
    [InlineData("maintain", 100.0, 95.0, RatingAction.TargetLower)]
    [InlineData("maintain", 100.0, 100.0, RatingAction.Maintain)]
    [InlineData("reiterate", null, 100.0, RatingAction.Reiterate)]
    [InlineData(null, 100.0, 105.0, RatingAction.TargetRaise)]       // target-only note
    [InlineData(null, null, 105.0, RatingAction.Maintain)]           // first target seen, nothing to compare
    public void ToAction_DerivesFromRawActionAndTargetMove(string? raw, double? previous, double? next, RatingAction expected) =>
        Assert.Equal(expected, RatingNormalizer.ToAction(raw, previous, next));
}
