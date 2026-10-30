using System.Text.Json;

namespace PriceHunt.Api.Streaming;

public sealed record SseEvent(string Event, object? Data);

public sealed class SseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpResponse _response;

    public SseWriter(HttpResponse response) => _response = response;

    public static void Prepare(HttpResponse response)
    {
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache, no-transform";
        response.Headers.Connection = "keep-alive";
        response.Headers["X-Accel-Buffering"] = "no";
    }

    public async Task WriteAsync(SseEvent sseEvent, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(sseEvent.Data, JsonOptions);
        await _response.WriteAsync($"event: {sseEvent.Event}\ndata: {payload}\n\n", cancellationToken);
        await _response.Body.FlushAsync(cancellationToken);
    }
}
