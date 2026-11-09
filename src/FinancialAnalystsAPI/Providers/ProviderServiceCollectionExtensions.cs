using FinancialAnalystsAPI.Providers.Fake;
using FinancialAnalystsAPI.Providers.Fmp;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FinancialAnalystsAPI.Providers;

public static class ProviderServiceCollectionExtensions
{
    /// <summary>
    /// Registers both providers and resolves <see cref="IAnalystDataProvider"/> from <c>DataProvider:Name</c>
    /// at resolution time, so test hosts can switch providers through configuration.
    /// </summary>
    public static IServiceCollection AddAnalystDataProvider(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<DataProviderOptions>()
            .BindConfiguration(DataProviderOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<FmpOptions>()
            .BindConfiguration(FmpOptions.Section)
            .ValidateDataAnnotations()
            .Validate<IOptions<DataProviderOptions>>(
                (fmp, provider) => provider.Value.Name != DataProviderOptions.Fmp || !string.IsNullOrWhiteSpace(fmp.ApiKey),
                "Fmp:ApiKey is required when DataProvider:Name is \"Fmp\". Locally: dotnet user-secrets set \"Fmp:ApiKey\" \"<key>\" " +
                "--project src/FinancialAnalystsAPI. In deployment: set the Fmp__ApiKey environment variable. " +
                "Or set DataProvider:Name to \"Fake\" for offline synthetic data.")
            .Validate<IOptions<DataProviderOptions>>(
                (fmp, provider) => provider.Value.Name != DataProviderOptions.Fmp || !LooksMalformed(fmp.ApiKey),
                "Fmp:ApiKey contains spaces, quotes, angle brackets or braces, so it was probably stored with part of a " +
                "placeholder such as \"<key>\". Set it again as the bare key: " +
                "dotnet user-secrets set \"Fmp:ApiKey\" YOURKEY --project src/FinancialAnalystsAPI")
            .ValidateOnStart();

        services.AddSingleton(sp => new DailyCallBudget(
            sp.GetRequiredService<IOptions<FmpOptions>>().Value.DailyCallBudget,
            sp.GetRequiredService<TimeProvider>()));
        services.AddTransient<DailyCallBudgetHandler>();
        services.AddSingleton<FmpPlanRestrictions>();

        var fmpClient = services.AddHttpClient<FmpAnalystDataProvider>((sp, client) =>
            {
                var baseUrl = sp.GetRequiredService<IOptions<FmpOptions>>().Value.BaseUrl;
                client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
            })
            .RemoveAllLoggers(); // request logs include the URL, and the URL includes the API key

        // Handlers run outermost-first in registration order: the budget sits inside the resilience
        // pipeline, so every retry attempt is counted against the daily budget.
        fmpClient.AddStandardResilienceHandler();
        fmpClient.AddHttpMessageHandler<DailyCallBudgetHandler>();

        services.AddSingleton<FakeAnalystDataProvider>();

        services.AddScoped<IAnalystDataProvider>(sp =>
            sp.GetRequiredService<IOptions<DataProviderOptions>>().Value.Name == DataProviderOptions.Fake
                ? sp.GetRequiredService<FakeAnalystDataProvider>()
                : sp.GetRequiredService<FmpAnalystDataProvider>());

        return services;
    }

    /// <summary>API keys are opaque tokens; these characters only appear when a placeholder or quotes were pasted in.</summary>
    internal static bool LooksMalformed(string? apiKey) =>
        apiKey is not null && apiKey.Any(c => char.IsWhiteSpace(c) || c is '"' or '\'' or '<' or '>' or '{' or '}');
}
