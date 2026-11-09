using System.Reflection;
using System.Text.Json.Serialization;
using FinancialAnalystsAPI.Api;
using FinancialAnalystsAPI.Auth;
using FinancialAnalystsAPI.Cli;
using FinancialAnalystsAPI.Consensus;
using FinancialAnalystsAPI.Data;
using FinancialAnalystsAPI.Ingestion;
using FinancialAnalystsAPI.Models;
using FinancialAnalystsAPI.Providers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

// `dotnet run -- ingest|apikey ...` runs one command and exits instead of starting the web server.
var isCommand = Commands.TryParse(args, out var command, out var hostArgs);

var builder = WebApplication.CreateBuilder(hostArgs);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();

builder.Services.AddDbContext<AnalystsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("AnalystsDb")));

// Data and ingestion
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
builder.Services.AddSingleton<IngestionCoordinator>();
if (!isCommand) builder.Services.AddHostedService<IngestionWorker>();

builder.Services.AddScoped<ConsensusService>();

// API keys: anonymous reads are allowed; admin endpoints need an Admin key.
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ApiClientStore>();
builder.Services.AddAuthentication(ApiKeys.Scheme)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeys.Scheme, null);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(ApiKeys.AdminPolicy, p => p.RequireRole(nameof(ApiClientRole.Admin)));

builder.Services.AddApiCachingAndRateLimits();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Financial Analysts API",
        Version = "v1",
        Description = "Wall Street analyst ratings, consensus and activity. Reads are open (rate-limited per IP); " +
                      $"send an API key in {ApiKeys.HeaderName} for a higher limit and for admin endpoints.",
    });
    o.AddSecurityDefinition(ApiKeys.Scheme, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = ApiKeys.HeaderName,
        Description = "API key created with `dotnet run -- apikey create <name> [--admin]`.",
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = ApiKeys.Scheme } }] = [],
    });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) o.IncludeXmlComments(xml);
});

var app = builder.Build();

if (isCommand) return await command!.RunAsync(app.Services);

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseMiddleware<RejectInvalidKeysMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();   // after authentication, so limits are per key; before the cache, so cached hits still count
app.UseOutputCache();
app.MapControllers();

app.Run();
return 0;

// Exposes the implicit Program class to WebApplicationFactory in the test project.
public partial class Program;
