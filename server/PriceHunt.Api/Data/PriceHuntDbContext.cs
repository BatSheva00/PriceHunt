using Microsoft.EntityFrameworkCore;
using PriceHunt.Api.Models;

namespace PriceHunt.Api.Data;

public class PriceHuntDbContext : DbContext
{
    public PriceHuntDbContext(DbContextOptions<PriceHuntDbContext> options) : base(options)
    {
    }

    public DbSet<SearchRun> SearchRuns => Set<SearchRun>();
    public DbSet<SupplierQuote> Quotes => Set<SupplierQuote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SearchRun>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FromLocation).HasMaxLength(120);
            entity.Property(x => x.ToLocation).HasMaxLength(120);
            entity.Property(x => x.SupplierNamesCsv).HasMaxLength(400);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.StartedAt);
        });

        modelBuilder.Entity<SupplierQuote>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SupplierName).HasMaxLength(60);
            entity.Property(x => x.Currency).HasMaxLength(3);

            // SQLite has no native decimal. REAL keeps numeric sort; the C# type stays decimal.
            entity.Property(x => x.Price).HasConversion<double>().HasColumnType("REAL");

            entity.HasOne(x => x.SearchRun)
                .WithMany(run => run.Quotes)
                .HasForeignKey(x => x.SearchRunId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.SearchRunId, x.SupplierName }).IsUnique();
            entity.HasIndex(x => x.SearchStartedAt);
            entity.HasIndex(x => new { x.FromLocation, x.ToLocation, x.SearchStartedAt });
            entity.HasIndex(x => new { x.SupplierName, x.SearchStartedAt });
        });
    }
}
