using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertGroupGovernance;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface ITeacherInterestService
{
    Task<TeacherInterestRequestDto> SubmitAsync(SubmitTeacherInterestRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TeacherInterestRequestDto>> ListForGroupAsync(Guid groupId, CancellationToken ct = default);
}

public class TeacherInterestService(IApplicationDbContext db, IExpertGroupService groups) : ITeacherInterestService
{
    public async Task<TeacherInterestRequestDto> SubmitAsync(SubmitTeacherInterestRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email))
            throw new InvalidOperationException("Nom et courriel requis.");

        var entity = new TeacherInterestRequest
        {
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            CountryCode = string.IsNullOrWhiteSpace(request.CountryCode)
                ? null
                : request.CountryCode.Trim().ToUpperInvariant(),
            City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim(),
            Disciplines = string.IsNullOrWhiteSpace(request.Disciplines) ? null : request.Disciplines.Trim(),
            Experience = string.IsNullOrWhiteSpace(request.Experience) ? null : request.Experience.Trim(),
            Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
            Status = TeacherInterestRequestStatus.Submitted
        };

        var reviewer = groups.ResolveReviewerGroup(entity.CountryCode);
        if (reviewer is not null)
        {
            entity.RoutedExpertGroupId = reviewer.Id;
            entity.Status = TeacherInterestRequestStatus.Routed;
        }

        db.Add(entity);
        await db.SaveChangesAsync(ct);

        return new TeacherInterestRequestDto(
            entity.Id, entity.FullName, entity.Email, entity.CountryCode, entity.City,
            entity.Disciplines, entity.Experience, entity.Message, entity.RoutedExpertGroupId,
            reviewer?.Name, entity.Status, entity.CreatedAt);
    }

    public Task<IReadOnlyList<TeacherInterestRequestDto>> ListForGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        var groupName = db.ExpertGroups.FirstOrDefault(g => g.Id == groupId)?.Name;
        IReadOnlyList<TeacherInterestRequestDto> list = db.TeacherInterestRequests
            .Where(r => r.RoutedExpertGroupId == groupId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new TeacherInterestRequestDto(
                r.Id, r.FullName, r.Email, r.CountryCode, r.City, r.Disciplines, r.Experience,
                r.Message, r.RoutedExpertGroupId, groupName, r.Status, r.CreatedAt))
            .ToList();
        return Task.FromResult(list);
    }
}
