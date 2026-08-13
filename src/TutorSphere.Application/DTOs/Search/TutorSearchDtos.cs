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
    decimal? MinRating = null,
    /// <summary>Pays du spectateur (ISO) — filtre de visibilité géographique (optionnel).</summary>
    string? ViewerCountry = null,
    /// <summary>Filtrer par groupe d'experts ayant approuvé l'enseignant ; null = tous.</summary>
    Guid? ExpertGroupId = null);

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
    decimal? Rating,
    string? PhotoUrl = null,
    int CurrentStudentCount = 0,
    decimal WeeklyHours = 0m,
    IReadOnlyList<string>? Levels = null,
    IReadOnlyList<string>? Specialties = null,
    IReadOnlyList<string>? Languages = null,
    int? SessionDurationMin = null,
    bool IsVerified = false,
    bool HasFlexibleSessions = false,
    Guid? ExpertGroupId = null,
    string? ExpertGroupName = null);

/// <summary>Option de filtre publique pour l'annuaire.</summary>
public record ExpertGroupSearchOptionDto(
    Guid Id,
    string Name,
    string? CountryCode,
    bool IsInternational);
