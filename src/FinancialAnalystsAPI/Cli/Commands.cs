using FinancialAnalystsAPI.Auth;
using FinancialAnalystsAPI.Ingestion;
using FinancialAnalystsAPI.Models;
using Microsoft.Extensions.Options;

namespace FinancialAnalystsAPI.Cli;

/// <summary>
/// One-shot commands that run instead of the web server:
/// <code>
/// ingest [--backfill]
/// apikey create &lt;name&gt; [--admin]
/// apikey list
/// apikey revoke &lt;name&gt;
/// </code>
/// Arguments of the form <c>--Key=Value</c> are passed through to configuration.
/// </summary>
internal sealed class Commands
{
    private readonly string[] _tokens;

    private Commands(string[] tokens) => _tokens = tokens;

    public static bool TryParse(string[] args, out Commands? commands, out string[] hostArgs)
    {
        // Configuration overrides look like --Key=Value; everything else belongs to the command.
        var tokens = args.Where(a => !(a.StartsWith("--") && a.Contains('='))).ToArray();
        hostArgs = args.Except(tokens).ToArray();

        if (tokens.FirstOrDefault() is "ingest" or "apikey")
        {
            commands = new Commands(tokens);
            return true;
        }

        commands = null;
        hostArgs = args;
        return false;
    }

    public async Task<int> RunAsync(IServiceProvider services)
    {
        // The host never starts in this mode, so run the ValidateOnStart checks here.
        try
        {
            services.GetService<IStartupValidator>()?.Validate();
        }
        catch (OptionsValidationException ex)
        {
            Console.Error.WriteLine($"Configuration error: {string.Join(" ", ex.Failures)}");
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        switch (_tokens)
        {
            case ["ingest", .. var rest] when rest.All(r => r == "--backfill"):
            {
                var run = await sp.GetRequiredService<IngestionService>().RunAsync(rest.Length > 0, CancellationToken.None);
                Console.WriteLine($"Ingestion {run.Status}: {run.StocksProcessed} stocks, +{run.PricesAdded} prices, " +
                                  $"+{run.RatingsAdded} ratings, {run.RatingsUpdated} ratings updated.");
                if (run.Error is not null) Console.Error.WriteLine(run.Error);
                return run.Status == IngestionStatus.Failed ? 1 : 0;
            }

            case ["apikey", "create", var name, .. var flags] when flags.All(f => f == "--admin"):
            {
                var role = flags.Length > 0 ? ApiClientRole.Admin : ApiClientRole.Reader;
                try
                {
                    var key = await sp.GetRequiredService<ApiClientStore>().CreateAsync(name, role, CancellationToken.None);
                    Console.WriteLine($"Created {role} key for '{name}'. Store it now; it can't be shown again:");
                    Console.WriteLine(key);
                    return 0;
                }
                catch (InvalidOperationException ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    return 1;
                }
            }

            case ["apikey", "list"]:
            {
                var clients = await sp.GetRequiredService<ApiClientStore>().ListAsync(CancellationToken.None);
                if (clients.Count == 0) Console.WriteLine("No API keys.");
                foreach (var c in clients)
                {
                    var state = c.IsActive ? "active" : $"revoked {c.RevokedAtUtc:yyyy-MM-dd}";
                    Console.WriteLine($"{c.Name,-24} {c.Role,-7} {c.KeyPrefix}…  created {c.CreatedAtUtc:yyyy-MM-dd}  {state}");
                }
                return 0;
            }

            case ["apikey", "revoke", var name]:
            {
                var revoked = await sp.GetRequiredService<ApiClientStore>().RevokeAsync(name, CancellationToken.None);
                Console.WriteLine(revoked ? $"Revoked '{name}'." : $"No active key named '{name}'.");
                return revoked ? 0 : 1;
            }

            default:
                Console.Error.WriteLine("""
                    Usage:
                      ingest [--backfill]
                      apikey create <name> [--admin]
                      apikey list
                      apikey revoke <name>
                    """);
                return 2;
        }
    }
}
