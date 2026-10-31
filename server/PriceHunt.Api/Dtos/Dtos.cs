namespace PriceHunt.Api.Dtos;

public sealed record PlaceDto(string Id, string Name);

public sealed record SearchRequestDto(
    string FromLocation,
    string ToLocation,
    DateTime FromDate,
    DateTime ToDate,
    List<string>? Suppliers);

public sealed record SearchCreatedDto(Guid SearchId, IReadOnlyList<string> Suppliers, int DeadlineMs);

public sealed record RunStartedDto(Guid SearchId, int Total, IReadOnlyList<string> Suppliers);

public sealed record QuoteDto(string Supplier, decimal Price, int ResponseTimeMs, string Status, DateTime At);

public sealed record ProgressDto(int Responded, int Total);

public sealed record RunCompletedDto(string Status, int Responded, int Total, long DurationMs);

public sealed record HistoryRowDto(
    Guid Id,
    DateTime Date,
    string Route,
    string Supplier,
    decimal? Price,
    int ResponseTimeMs);

public sealed record HistoryPageDto(IReadOnlyList<HistoryRowDto> Items, int Total, int Page, int PageSize);
