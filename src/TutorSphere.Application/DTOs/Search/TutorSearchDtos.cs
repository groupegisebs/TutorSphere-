using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.Search;

public record TutorSearchFilters(
    string? Subject = null,
    string? City = null,
    string? Language = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Level = null,
    LessonMode? Mode = null,
    decimal? MinRating = null);

public record TutorSearchResultDto(
    Guid Id,
    string Name,
    string Slug,
    string? City,
    string? Country,
    string? Description,
    string Language,
    string Currency,
    decimal? MinPrice,
    decimal? MaxPrice,
    IReadOnlyList<string> Subjects,
    IReadOnlyList<string> Modes,
    decimal? Rating);
