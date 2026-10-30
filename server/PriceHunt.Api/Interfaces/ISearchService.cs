using PriceHunt.Api.Dtos;

namespace PriceHunt.Api.Interfaces;

public interface ISearchService
{
    Task<(SearchCreatedDto? Created, SearchRejection? Rejection)> CreateAsync(SearchRequestDto? body);

    Task<SearchRejection?> StreamAsync(Guid id, HttpContext http);

    Task<SearchRejection?> CancelAsync(Guid id);
}
