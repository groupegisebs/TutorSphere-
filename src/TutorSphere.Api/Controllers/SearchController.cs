using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.Search;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService) => _searchService = searchService;

    [HttpGet("tutors")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<TutorSearchResultDto>>> SearchTutors(
        [FromQuery] string? subject,
        [FromQuery] string? city,
        [FromQuery] string? language,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? level,
        [FromQuery] LessonMode? mode,
        [FromQuery] decimal? minRating,
        CancellationToken ct)
    {
        var filters = new TutorSearchFilters(
            subject, city, language, minPrice, maxPrice, level, mode, minRating);

        var results = await _searchService.SearchTutorsAsync(filters, ct);
        return Ok(results);
    }
}
