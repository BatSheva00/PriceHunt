namespace PriceHunt.Api.Models;

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

    // Copied from the run so a later history query can filter and sort without a join.
    public string FromLocation { get; set; } = string.Empty;
    public string ToLocation { get; set; } = string.Empty;
    public DateTime SearchStartedAt { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}
