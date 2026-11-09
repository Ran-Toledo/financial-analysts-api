using FinancialAnalystsAPI.Data;
using FinancialAnalystsAPI.Ingestion;
using FinancialAnalystsAPI.Models;
using FinancialAnalystsAPI.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// `dotnet run -- ingest [--backfill]` runs one ingestion and exits instead of starting the web server.
var ingestCommand = args.FirstOrDefault() == "ingest";
var backfill = ingestCommand && args.Contains("--backfill");
var hostArgs = ingestCommand ? args.Where(a => a is not ("ingest" or "--backfill")).ToArray() : args;

var builder = WebApplication.CreateBuilder(hostArgs);

builder.Services.AddControllers();
builder.Services.AddDbContext<AnalystsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("AnalystsDb")));

builder.Services.AddAnalystDataProvider();

builder.Services.AddOptions<TrackingOptions>()
    .BindConfiguration(TrackingOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<IngestionOptions>()
    .BindConfiguration(IngestionOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddScoped<IngestionService>();
if (!ingestCommand) builder.Services.AddHostedService<IngestionWorker>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (ingestCommand)
{
    // The host never starts in this mode, so run the ValidateOnStart checks here.
    try
    {
        app.Services.GetService<IStartupValidator>()?.Validate();
    }
    catch (OptionsValidationException ex)
    {
        Console.Error.WriteLine($"Configuration error: {string.Join(" ", ex.Failures)}");
        return 1;
    }

    await using var scope = app.Services.CreateAsyncScope();
    var run = await scope.ServiceProvider.GetRequiredService<IngestionService>().RunAsync(backfill, CancellationToken.None);

    Console.WriteLine($"Ingestion {run.Status}: {run.StocksProcessed} stocks, +{run.PricesAdded} prices, " +
                      $"+{run.RatingsAdded} ratings, {run.RatingsUpdated} ratings updated.");
    if (run.Error is not null) Console.Error.WriteLine(run.Error);
    return run.Status == IngestionStatus.Failed ? 1 : 0;
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
return 0;

// Exposes the implicit Program class to WebApplicationFactory in the test project.
public partial class Program;
