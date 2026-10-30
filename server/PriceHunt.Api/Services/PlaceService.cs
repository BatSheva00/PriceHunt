using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PriceHunt.Api.Dtos;
using PriceHunt.Api.Interfaces;

namespace PriceHunt.Api.Services;

public sealed class PlaceService : IPlaceService
{
    private const string FirstPage =
        "api/3/action/datastore_search?resource_id=5c78e9fa-c2e2-4771-93ff-7f400a12f7ba&limit=1500";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _http;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<City>? _cities;

    public PlaceService(IHttpClientFactory http) => _http = http;

    public async Task<IReadOnlyList<PlaceDto>> SearchAsync(string? query, CancellationToken cancellationToken)
    {
        var q = query?.Trim() ?? string.Empty;
        if (q.Length == 0)
        {
            return [];
        }

        var cities = await LoadAsync(cancellationToken);
        return cities
            .Where(city =>
                city.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                city.LocalName.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(city => Starts(city, q) ? 0 : 1)
            .ThenBy(city => city.Name, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select(city => new PlaceDto(city.Id, city.Name))
            .ToArray();
    }

    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken)
    {
        var cities = await LoadAsync(cancellationToken);
        return cities.Any(city => string.Equals(city.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<City>> LoadAsync(CancellationToken cancellationToken)
    {
        if (_cities is not null)
        {
            return _cities;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cities is not null)
            {
                return _cities;
            }

            var client = _http.CreateClient("places");
            var url = FirstPage;
            var cities = new List<City>();

            for (var page = 0; page < 5 && !string.IsNullOrEmpty(url); page++)
            {
                var body = await client.GetFromJsonAsync<GovPage>(url, Json, cancellationToken);
                var result = body?.Result;
                if (body is not { Success: true } || result is null)
                {
                    throw new InvalidOperationException("The city list could not be loaded.");
                }

                cities.AddRange(result.Records.Select(ToCity).OfType<City>());
                if (cities.Count >= result.Total)
                {
                    break;
                }

                url = result.Links?.Next ?? string.Empty;
            }

            _cities = cities;
            return _cities;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static City? ToCity(GovRecord record)
    {
        var local = record.Name?.Trim() ?? string.Empty;
        var english = record.EnglishName?.Trim() ?? string.Empty;
        var name = english.Length > 0 ? Title(english) : local;
        if (string.IsNullOrWhiteSpace(record.Code) || name.Length == 0)
        {
            return null;
        }

        return new City(record.Code, name, local);
    }

    private static bool Starts(City city, string query) =>
        city.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ||
        city.LocalName.StartsWith(query, StringComparison.OrdinalIgnoreCase);

    private static string Title(string value)
    {
        var chars = value.ToLowerInvariant().ToCharArray();
        var capital = true;
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsLetter(chars[i]))
            {
                if (capital)
                {
                    chars[i] = char.ToUpperInvariant(chars[i]);
                }

                capital = false;
            }
            else
            {
                capital = chars[i] is ' ' or '-';
            }
        }

        return new string(chars);
    }

    private sealed record City(string Id, string Name, string LocalName);

    private sealed class GovPage
    {
        public bool Success { get; set; }
        public GovResult? Result { get; set; }
    }

    private sealed class GovResult
    {
        public List<GovRecord> Records { get; set; } = [];
        public int Total { get; set; }

        [JsonPropertyName("_links")]
        public GovLinks? Links { get; set; }
    }

    private sealed class GovLinks
    {
        public string? Next { get; set; }
    }

    private sealed class GovRecord
    {
        [JsonPropertyName("סמל_ישוב")]
        public string? Code { get; set; }

        [JsonPropertyName("שם_ישוב")]
        public string? Name { get; set; }

        [JsonPropertyName("שם_ישוב_לועזי")]
        public string? EnglishName { get; set; }
    }
}
