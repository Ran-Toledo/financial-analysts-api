using Microsoft.EntityFrameworkCore;
using FinancialAnalystsAPI.Models;

namespace FinancialAnalystsAPI.Data
{
    public class AnalystsDbContext : DbContext
    {
        public AnalystsDbContext(DbContextOptions<AnalystsDbContext> options) : base(options) { }

        public DbSet<FinancialAnalyst> FinancialAnalysts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FinancialAnalyst>().ToTable("FinancialAnalysts");
        }
    }
}
