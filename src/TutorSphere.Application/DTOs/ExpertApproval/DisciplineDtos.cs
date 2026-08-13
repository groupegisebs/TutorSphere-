using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.ExpertApproval;

public record DisciplineServiceItemDto(
    Guid Id,
    string Title,
    string? Description,
    int SortOrder);

/// <summary>Service fourni dans le cadre d'une discipline (Id nul = nouvelle ligne).</summary>
public record DisciplineServiceItemInput(
    Guid? Id,
    string Title,
    string? Description,
    int SortOrder = 0);

public record DisciplineDto(
    Guid Id,
    Guid ExpertGroupId,
    string Name,
    SchoolCycle Cycle,
    string? WorkMethod,
    bool IsActive,
    IReadOnlyList<DisciplineServiceItemDto> Services,
    int AssignedTeacherCount,
    DateTime CreatedAt);

/// <summary>Discipline du groupe avec statut d'affectation pour un enseignant donné.</summary>
public record TeacherDisciplineStatusDto(
    Guid DisciplineId,
    string Name,
    SchoolCycle Cycle,
    bool IsActive,
    bool IsAssigned);

public record CreateDisciplineRequest(
    string Name,
    SchoolCycle Cycle,
    string? WorkMethod,
    IReadOnlyList<DisciplineServiceItemInput>? Services = null);

public record UpdateDisciplineRequest(
    string Name,
    SchoolCycle Cycle,
    string? WorkMethod,
    bool IsActive,
    IReadOnlyList<DisciplineServiceItemInput>? Services = null);

/// <summary>Enseignant approuvé du groupe, avec statut d'affectation pour une discipline donnée.</summary>
public record GroupTeacherAssignmentDto(
    Guid TenantId,
    string SchoolName,
    string? OwnerEmail,
    string? OwnerName,
    bool IsAssigned);

/// <summary>Discipline publique (visible sur la fiche enseignant) : services + méthode de travail.</summary>
public record PublicDisciplineServiceDto(string Title, string? Description);

public record PublicDisciplineDto(
    Guid Id,
    string Name,
    string Cycle,
    string? WorkMethod,
    IReadOnlyList<PublicDisciplineServiceDto> Services);
