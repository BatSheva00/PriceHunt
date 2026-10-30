using Microsoft.AspNetCore.Mvc;
using PriceHunt.Api.Dtos;
using PriceHunt.Api.Interfaces;

namespace PriceHunt.Api.Controllers;

[ApiController]
[Route("api/places")]
public sealed class PlacesController : ControllerBase
{
    private readonly IPlaceService _places;

    public PlacesController(IPlaceService places) => _places = places;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlaceDto>>> Get([FromQuery] string? q, CancellationToken cancellationToken) =>
        Ok(await _places.SearchAsync(q, cancellationToken));
}
