using FinancialAnalystsAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancialAnalystsAPI.Data;

public class AnalystsDbContext(DbContextOptions<AnalystsDbContext> options) : DbContext(options)
{
    public DbSet<Firm> Firms => Set<Firm>();
    public DbSet<Analyst> Analysts => Set<Analyst>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<PriceSnapshot> PriceSnapshots => Set<PriceSnapshot>();
    public DbSet<IngestionRun> IngestionRuns => Set<IngestionRun>();
    public DbSet<ApiClient> ApiClients => Set<ApiClient>();
    public DbSet<CallOutcome> CallOutcomes => Set<CallOutcome>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AnalystsDbContext).Assembly);
    }
}
