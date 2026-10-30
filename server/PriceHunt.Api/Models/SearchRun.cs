namespace PriceHunt.Api.Models;

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
