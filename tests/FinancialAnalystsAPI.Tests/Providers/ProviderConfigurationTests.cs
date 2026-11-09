using FinancialAnalystsAPI.Providers;
using FinancialAnalystsAPI.Providers.Fmp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinancialAnalystsAPI.Tests.Providers;

public class ProviderConfigurationTests
{
    [Theory]
    [InlineData("<abc123>", true)]
    [InlineData("\"abc123\"", true)]
    [InlineData("'abc123'", true)]
    [InlineData("abc 123", true)]
    [InlineData("{abc123}", true)]
    [InlineData("AbC123xyz", false)]
    [InlineData(null, false)] // "missing" is reported by the required-key check instead
    public void LooksMalformed_FlagsPlaceholderAndQuoteCharacters(string? key, bool expected) =>
        Assert.Equal(expected, ProviderServiceCollectionExtensions.LooksMalformed(key));

    [Theory]
    [InlineData(null, "Fmp:ApiKey is required")]
    [InlineData("<abc123>", "probably stored with part of a placeholder")]
    public void FmpProvider_RejectsMissingOrMalformedKey(string? key, string expectedMessage)
    {
        var options = BuildServices(("DataProvider:Name", "Fmp"), ("Fmp:ApiKey", key))
            .GetRequiredService<IOptions<FmpOptions>>();

        var ex = Assert.Throws<OptionsValidationException>(() => options.Value);
        Assert.Contains(ex.Failures, f => f.Contains(expectedMessage));
    }

    [Fact]
    public void FakeProvider_NeedsNoKey()
    {
        var services = BuildServices(("DataProvider:Name", "Fake"), ("Fmp:ApiKey", null));

        Assert.Null(services.GetRequiredService<IOptions<FmpOptions>>().Value.ApiKey);
        using var scope = services.CreateScope();
        Assert.Equal("fake", scope.ServiceProvider.GetRequiredService<IAnalystDataProvider>().Name);
    }

    private static ServiceProvider BuildServices(params (string Key, string? Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddAnalystDataProvider()
            .BuildServiceProvider();
    }
}
