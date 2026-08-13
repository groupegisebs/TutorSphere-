using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.Common;
using TutorSphere.Application.DTOs.Search;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;
    private readonly IParentService _parentService;
    private readonly IStudentPortalService _studentPortal;

    public SearchController(
        ISearchService searchService,
        IParentService parentService,
        IStudentPortalService studentPortal)
    {
        _searchService = searchService;
        _parentService = parentService;
        _studentPortal = studentPortal;
    }

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
        [FromQuery] string? viewerCountry,
        CancellationToken ct)
    {
        var effectiveCountry = await ResolveViewerCountryAsync(viewerCountry, ct);

        var filters = new TutorSearchFilters(
            subject, city, language, minPrice, maxPrice, level, mode, minRating, effectiveCountry);

        var results = await _searchService.SearchTutorsAsync(filters, ct);
        return Ok(results);
    }

    /// <summary>
    /// Parent / élève : le pays du profil prime (sécurité). Sinon le paramètre query.
    /// </summary>
    private async Task<string?> ResolveViewerCountryAsync(string? requested, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            if (User.IsInRole(UserRoles.Parent))
            {
                var parent = await _parentService.GetByUserIdAsync(userId, ct);
                var fromProfile = ProfileVisibility.NormalizeCode(parent?.Country);
                if (fromProfile.Length == 2)
                    return fromProfile;
            }
            else if (User.IsInRole(UserRoles.Student))
            {
                var fromProfile = ProfileVisibility.NormalizeCode(
                    await _studentPortal.GetViewerCountryAsync(userId, ct));
                if (fromProfile.Length == 2)
                    return fromProfile;
            }
        }

        var fromQuery = ProfileVisibility.NormalizeCode(requested);
        return fromQuery.Length == 2 ? fromQuery : null;
    }
}
