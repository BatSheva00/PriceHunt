using Microsoft.EntityFrameworkCore;

namespace PriceHunt.Api.Data;

public enum SearchStatus
{
    Running = 0,
    Completed = 1,
    TimedOut = 2,
    Cancelled = 3
}

public enum QuoteStatus
{
    Success = 0,
    Failed = 1,
    Timeout = 2,
    NoResponse = 3
}

public class SearchRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FromLocation { get; set; } = string.Empty;
    public string ToLocation { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string SupplierNamesCsv { get; set; } = string.Empty;
    public SearchStatus Status { get; set; } = SearchStatus.Running;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public int TotalSuppliers { get; set; }
    public int RespondedCount { get; set; }
    public List<SupplierQuote> Quotes { get; set; } = new();
}

public class SupplierQuote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SearchRunId { get; set; }
    public SearchRun? SearchRun { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "ILS";
    public int ResponseTimeMs { get; set; }
    public DateTime RespondedAt { get; set; } = DateTime.UtcNow;
    public QuoteStatus Status { get; set; }

    // Copied from the run on purpose: the history screen is a flat, filtered, sorted,
    // paginated table, and denormalising keeps that a single indexed query instead of
    // a join followed by in-memory sorting.
    public string FromLocation { get; set; } = string.Empty;
    public string ToLocation { get; set; } = string.Empty;
    public DateTime SearchStartedAt { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

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

            // SQLite has no native decimal: EF stores it as TEXT, which makes ORDER BY and
            // aggregation behave lexicographically. Mapping it to REAL keeps C# decimals in
            // the model while letting the database sort numerically.
            entity.Property(x => x.Price).HasConversion<double>().HasColumnType("REAL");

            entity.HasOne(x => x.SearchRun)
                .WithMany(run => run.Quotes)
                .HasForeignKey(x => x.SearchRunId)
                .OnDelete(DeleteBehavior.Cascade);

            // A supplier answers once per run: the unique index makes a duplicate row impossible
            // even if two streams for the same run were ever started by mistake.
            entity.HasIndex(x => new { x.SearchRunId, x.SupplierName }).IsUnique();
            entity.HasIndex(x => x.SearchStartedAt);
            entity.HasIndex(x => new { x.FromLocation, x.ToLocation, x.SearchStartedAt });
            entity.HasIndex(x => new { x.SupplierName, x.SearchStartedAt });
        });
    }
}
