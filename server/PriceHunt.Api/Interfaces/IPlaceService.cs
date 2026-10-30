using PriceHunt.Api.Dtos;

namespace PriceHunt.Api.Interfaces;

public interface IPlaceService
{
    Task<IReadOnlyList<PlaceDto>> SearchAsync(string? query, CancellationToken cancellationToken);
}
