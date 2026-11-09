using FinancialAnalystsAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialAnalystsAPI.Data.Configurations;

// Prices and targets are double, not decimal: EF Core's SQLite provider cannot translate
// Sum/Average/Min/Max or ORDER BY over decimal, and consensus and scoring depend on those.

internal sealed class FirmConfiguration : IEntityTypeConfiguration<Firm>
{
    public void Configure(EntityTypeBuilder<Firm> b)
    {
        b.Property(f => f.Name).HasMaxLength(200);
        b.Property(f => f.NormalizedName).HasMaxLength(200);
        b.HasIndex(f => f.NormalizedName).IsUnique();
    }
}

internal sealed class AnalystConfiguration : IEntityTypeConfiguration<Analyst>
{
    public void Configure(EntityTypeBuilder<Analyst> b)
    {
        b.ToTable("Analysts");
        b.Property(a => a.Name).HasMaxLength(200);
        b.Property(a => a.NormalizedName).HasMaxLength(200);
        b.HasOne(a => a.Firm).WithMany(f => f.Analysts).HasForeignKey(a => a.FirmId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.FirmId, a.NormalizedName }).IsUnique();
    }
}

internal sealed class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> b)
    {
        b.Property(s => s.Ticker).HasMaxLength(16);
        b.Property(s => s.Name).HasMaxLength(200);
        b.Property(s => s.Sector).HasMaxLength(100);
        b.Property(s => s.Industry).HasMaxLength(200);
        b.Property(s => s.Exchange).HasMaxLength(50);
        b.HasIndex(s => s.Ticker).IsUnique();
        b.HasIndex(s => s.Sector);
    }
}

internal sealed class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> b)
    {
        b.Property(r => r.Action).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.Category).HasConversion<string>().HasMaxLength(10);
        b.Property(r => r.RawAction).HasMaxLength(50);
        b.Property(r => r.RatingFrom).HasMaxLength(100);
        b.Property(r => r.RatingTo).HasMaxLength(100);
        b.Property(r => r.Source).HasMaxLength(20);
        b.Property(r => r.SourceKey).HasMaxLength(300);

        b.HasOne(r => r.Stock).WithMany(s => s.Ratings).HasForeignKey(r => r.StockId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(r => r.Firm).WithMany(f => f.Ratings).HasForeignKey(r => r.FirmId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Analyst).WithMany(a => a.Ratings).HasForeignKey(r => r.AnalystId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(r => new { r.Source, r.SourceKey }).IsUnique();
        b.HasIndex(r => new { r.StockId, r.Date });
        b.HasIndex(r => new { r.AnalystId, r.Date });
        b.HasIndex(r => r.Date);
    }
}

internal sealed class PriceSnapshotConfiguration : IEntityTypeConfiguration<PriceSnapshot>
{
    public void Configure(EntityTypeBuilder<PriceSnapshot> b)
    {
        b.HasKey(p => new { p.StockId, p.Date });
        b.HasOne(p => p.Stock).WithMany(s => s.Prices).HasForeignKey(p => p.StockId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CallOutcomeConfiguration : IEntityTypeConfiguration<CallOutcome>
{
    public void Configure(EntityTypeBuilder<CallOutcome> b)
    {
        b.HasKey(o => new { o.RatingId, o.HorizonMonths });
        b.Property(o => o.Category).HasConversion<string>().HasMaxLength(10);
        b.HasOne(o => o.Rating).WithMany().HasForeignKey(o => o.RatingId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(o => new { o.HorizonMonths, o.EndDate });
    }
}

internal sealed class ApiClientConfiguration : IEntityTypeConfiguration<ApiClient>
{
    public void Configure(EntityTypeBuilder<ApiClient> b)
    {
        b.Property(c => c.Name).HasMaxLength(100);
        b.Property(c => c.KeyHash).HasMaxLength(64);
        b.Property(c => c.KeyPrefix).HasMaxLength(12);
        b.Property(c => c.Role).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(c => c.Name).IsUnique();
        b.HasIndex(c => c.KeyHash).IsUnique();
    }
}

internal sealed class IngestionRunConfiguration : IEntityTypeConfiguration<IngestionRun>
{
    public void Configure(EntityTypeBuilder<IngestionRun> b)
    {
        b.Property(r => r.Provider).HasMaxLength(20);
        b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.Error).HasMaxLength(4000);
        b.HasIndex(r => r.StartedAtUtc);
    }
}
