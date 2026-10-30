using Microsoft.AspNetCore.Mvc;
using PriceHunt.Api.Dtos;
using PriceHunt.Api.Interfaces;

namespace PriceHunt.Api.Controllers;

[ApiController]
[Route("api/search")]
public sealed class SearchController : ControllerBase
{
    private readonly ISearchService _search;

    public SearchController(ISearchService search) => _search = search;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SearchRequestDto? body)
    {
        var (created, rejection) = await _search.CreateAsync(body);
        if (rejection is not null)
        {
            return StatusCode(rejection.StatusCode, new { error = rejection.Error });
        }

        return Created($"/api/search/{created!.SearchId}", created);
    }

    [HttpGet("{id:guid}/stream")]
    public async Task Stream(Guid id)
    {
        var rejection = await _search.StreamAsync(id, HttpContext);
        if (rejection is null || Response.HasStarted)
        {
            return;
        }

        Response.StatusCode = rejection.StatusCode;
        await Response.WriteAsJsonAsync(new { error = rejection.Error });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var rejection = await _search.CancelAsync(id);
        if (rejection is not null)
        {
            return StatusCode(rejection.StatusCode);
        }

        return NoContent();
    }
}
