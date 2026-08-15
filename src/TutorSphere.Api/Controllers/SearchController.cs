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
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;
    private readonly IStudentPortalService _studentPortal;

    public SearchController(
        ISearchService searchService,
        IStudentPortalService studentPortal)
    {
        _searchService = searchService;
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
        [FromQuery] Guid? expertGroupId,
        CancellationToken ct)
    {
        var effectiveCountry = await ResolveViewerCountryAsync(viewerCountry, ct);

        var filters = new TutorSearchFilters(
            subject, city, language, minPrice, maxPrice, level, mode, minRating, effectiveCountry, expertGroupId);

        var results = await _searchService.SearchTutorsAsync(filters, ct);
        return Ok(results);
    }

    [HttpGet("expert-groups")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ExpertGroupSearchOptionDto>>> ListExpertGroups(CancellationToken ct) =>
        Ok(await _searchService.ListActiveExpertGroupsAsync(ct));

    /// <summary>
    /// Élève : pays du profil (ou query). Parent authentifié : pas de filtre pays
    /// (l'annuaire suit les filtres UI — groupe, matière, ville, prix, niveau).
    /// Anonyme : query optionnelle.
    /// </summary>
    private async Task<string?> ResolveViewerCountryAsync(string? requested, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            // Annuaire parent = toutes les fiches publiques éligibles + filtres explicites.
            if (User.IsInRole(UserRoles.Parent))
                return null;

            if (User.IsInRole(UserRoles.Student))
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
