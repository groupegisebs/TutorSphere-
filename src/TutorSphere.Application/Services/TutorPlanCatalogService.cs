using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Lessons;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface ITutorPlanCatalogService
{
    Task<IReadOnlyList<TutorPlanSubjectDto>> ListSubjectsAsync(CancellationToken ct = default);
}

/// <summary>
/// Matières de séance : disciplines assignées, offres de groupe et plans déjà matérialisés
/// pour l'enseignant courant.
/// </summary>
public class TutorPlanCatalogService(IApplicationDbContext db, ITenantContext tenant) : ITutorPlanCatalogService
{
    public Task<IReadOnlyList<TutorPlanSubjectDto>> ListSubjectsAsync(CancellationToken ct = default)
    {
        if (!tenant.HasTenant || tenant.TenantId is not Guid tenantId)
            throw new InvalidOperationException("Contexte locataire requis.");

        var subjects = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var assignedIds = db.TeacherDisciplineAssignments
            .Select(a => a.DisciplineId)
            .Distinct()
            .ToList();
        if (assignedIds.Count > 0)
        {
            foreach (var d in db.Disciplines.Where(d => assignedIds.Contains(d.Id) && d.IsActive))
                Add(subjects, d.Name, CycleLabel(d.Cycle));
        }

        var offerIds = db.GroupOfferTeachers
            .Where(a => a.TeacherTenantId == tenantId
                        && (a.AssignmentStatus == GroupOfferTeacherAssignmentStatus.Approved
                            || a.AssignmentStatus == GroupOfferTeacherAssignmentStatus.Active))
            .Select(a => a.GroupOfferId)
            .Distinct()
            .ToList();
        if (offerIds.Count > 0)
        {
            var offers = db.GroupOffers
                .Where(o => offerIds.Contains(o.Id) && o.Status == GroupOfferStatus.Published)
                .ToList();
            var discIds = offers.Where(o => o.DisciplineId.HasValue).Select(o => o.DisciplineId!.Value).Distinct().ToList();
            var discs = discIds.Count == 0
                ? new Dictionary<Guid, Domain.Entities.Discipline>()
                : db.Disciplines.Where(d => discIds.Contains(d.Id) && d.IsActive)
                    .ToList()
                    .ToDictionary(d => d.Id);

            foreach (var offer in offers)
            {
                if (offer.DisciplineId is Guid did && discs.TryGetValue(did, out var disc))
                    Add(subjects, disc.Name, CycleLabel(disc.Cycle));
                else
                    Add(subjects, offer.Name, offer.SchoolCycle);
            }
        }

        foreach (var offering in db.SubscriptionOfferings.Where(o => o.IsActive))
        {
            if (!string.IsNullOrWhiteSpace(offering.Subject))
                Add(subjects, offering.Subject.Trim(), null);
            else if (!string.IsNullOrWhiteSpace(offering.Title))
                Add(subjects, offering.Title.Trim(), null);
        }

        IReadOnlyList<TutorPlanSubjectDto> result = subjects
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new TutorPlanSubjectDto(kv.Key, kv.Value))
            .ToList();
        return Task.FromResult(result);
    }

    private static void Add(Dictionary<string, string?> subjects, string? name, string? cycle)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return;
        if (!subjects.TryGetValue(trimmed, out var existing) || string.IsNullOrWhiteSpace(existing))
            subjects[trimmed] = string.IsNullOrWhiteSpace(cycle) ? existing : cycle.Trim();
    }

    private static string CycleLabel(SchoolCycle cycle) => cycle switch
    {
        SchoolCycle.Primary => "Primaire",
        SchoolCycle.Secondary => "Secondaire",
        SchoolCycle.University => "Universitaire",
        SchoolCycle.AdultEducation => "Formation pour adultes",
        _ => cycle.ToString()
    };
}
