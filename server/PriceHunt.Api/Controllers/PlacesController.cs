using Microsoft.AspNetCore.Mvc;
using PriceHunt.Api.Interfaces;

namespace PriceHunt.Api.Controllers;

[ApiController]
[Route("api/places")]
public sealed class PlacesController : ControllerBase
{
    private readonly IPlaceService _places;

    public PlacesController(IPlaceService places) => _places = places;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? q, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _places.SearchAsync(q, cancellationToken));
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested
                                          && exception is HttpRequestException or TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
