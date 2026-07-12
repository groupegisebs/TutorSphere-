using TutorSphere.Application.DTOs.StudentSubscriptions;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IStudentSubscriptionService
{
    Task<StudentSubscriptionDto> EnrollAsync(string parentUserId, EnrollStudentRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<StudentSubscriptionDto>> GetForParentUserAsync(string parentUserId, CancellationToken ct = default);
    Task<IReadOnlyList<StudentSubscriptionDto>> GetForCurrentTenantAsync(CancellationToken ct = default);
    Task CancelAsync(string parentUserId, Guid subscriptionId, CancellationToken ct = default);
}

public class StudentSubscriptionService : IStudentSubscriptionService
{
    private readonly Common.Interfaces.IApplicationDbContext _db;

    public StudentSubscriptionService(Common.Interfaces.IApplicationDbContext db) => _db = db;

    public async Task<StudentSubscriptionDto> EnrollAsync(
        string parentUserId,
        EnrollStudentRequest request,
        CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == parentUserId)
            ?? throw new InvalidOperationException("Profil parent introuvable.");

        var student = _db.StudentsForAnyTenant.FirstOrDefault(s =>
                s.Id == request.StudentId && s.ParentProfileId == parent.Id)
            ?? throw new InvalidOperationException("Enfant introuvable.");

        var offering = _db.SubscriptionOfferingsForAnyTenant.FirstOrDefault(o =>
                o.Id == request.OfferingId && o.IsActive)
            ?? throw new InvalidOperationException("Offre introuvable ou inactive.");

        var duplicate = _db.StudentSubscriptionsForAnyTenant.Any(s =>
            s.StudentId == student.Id
            && s.OfferingId == offering.Id
            && (s.Status == SubscriptionStatus.Pending || s.Status == SubscriptionStatus.Active));
        if (duplicate)
            throw new InvalidOperationException("Cet enfant est déjà abonné (ou en cours d'abonnement) à cette offre.");

        // Ensure the child is linked to the tutor school for subsequent lessons.
        if (student.TenantId != offering.TenantId)
        {
            student.TenantId = offering.TenantId;
            student.UpdatedAt = DateTime.UtcNow;
        }

        var now = DateTime.UtcNow;
        var subscription = new Domain.Entities.StudentSubscription
        {
            TenantId = offering.TenantId,
            StudentId = student.Id,
            OfferingId = offering.Id,
            Status = SubscriptionStatus.Pending,
            StartDate = now,
            EndDate = now.AddDays(Math.Max(1, offering.DurationDays)),
            SessionsRemaining = Math.Max(0, offering.SessionCount)
        };

        _db.Add(subscription);
        await _db.SaveChangesAsync(ct);

        return Map(subscription, offering.Title, offering.Subject, offering.Price, offering.Currency,
            $"{student.FirstName} {student.LastName}".Trim(),
            $"{parent.FirstName} {parent.LastName}".Trim());
    }

    public Task<IReadOnlyList<StudentSubscriptionDto>> GetForParentUserAsync(
        string parentUserId,
        CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == parentUserId);
        if (parent is null)
            return Task.FromResult<IReadOnlyList<StudentSubscriptionDto>>([]);

        var childIds = _db.StudentsForAnyTenant
            .Where(s => s.ParentProfileId == parent.Id)
            .Select(s => s.Id)
            .ToList();

        var parentName = $"{parent.FirstName} {parent.LastName}".Trim();
        var subs = _db.StudentSubscriptionsForAnyTenant
            .Where(s => childIds.Contains(s.StudentId))
            .OrderByDescending(s => s.CreatedAt)
            .ToList();

        var offeringIds = subs.Select(s => s.OfferingId).Distinct().ToList();
        var offerings = _db.SubscriptionOfferingsForAnyTenant
            .Where(o => offeringIds.Contains(o.Id))
            .ToDictionary(o => o.Id);

        var students = _db.StudentsForAnyTenant
            .Where(s => childIds.Contains(s.Id))
            .ToDictionary(s => s.Id);

        var result = subs.Select(s =>
        {
            offerings.TryGetValue(s.OfferingId, out var offering);
            students.TryGetValue(s.StudentId, out var student);
            return Map(
                s,
                offering?.Title ?? "Offre",
                offering?.Subject,
                offering?.Price ?? 0,
                offering?.Currency ?? "CAD",
                student is null ? "" : $"{student.FirstName} {student.LastName}".Trim(),
                parentName);
        }).ToList();

        return Task.FromResult<IReadOnlyList<StudentSubscriptionDto>>(result);
    }

    public Task<IReadOnlyList<StudentSubscriptionDto>> GetForCurrentTenantAsync(CancellationToken ct = default)
    {
        var subs = _db.StudentSubscriptions
            .OrderByDescending(s => s.CreatedAt)
            .ToList();

        if (subs.Count == 0)
            return Task.FromResult<IReadOnlyList<StudentSubscriptionDto>>([]);

        var offeringIds = subs.Select(s => s.OfferingId).Distinct().ToList();
        var studentIds = subs.Select(s => s.StudentId).Distinct().ToList();

        var offerings = _db.SubscriptionOfferings
            .Where(o => offeringIds.Contains(o.Id))
            .ToDictionary(o => o.Id);

        // Élèves peuvent appartenir à un autre tenant avant rattachement — IgnoreQueryFilters.
        var students = _db.StudentsForAnyTenant
            .Where(s => studentIds.Contains(s.Id))
            .ToList();

        var parentIds = students
            .Where(s => s.ParentProfileId.HasValue)
            .Select(s => s.ParentProfileId!.Value)
            .Distinct()
            .ToList();

        var parents = _db.ParentProfilesForAnyTenant
            .Where(p => parentIds.Contains(p.Id))
            .ToDictionary(p => p.Id);

        var studentsById = students.ToDictionary(s => s.Id);

        var result = subs.Select(s =>
        {
            offerings.TryGetValue(s.OfferingId, out var offering);
            studentsById.TryGetValue(s.StudentId, out var student);
            string? parentName = null;
            if (student?.ParentProfileId is Guid pid && parents.TryGetValue(pid, out var parent))
                parentName = $"{parent.FirstName} {parent.LastName}".Trim();

            return Map(
                s,
                offering?.Title ?? "Offre",
                offering?.Subject,
                offering?.Price ?? 0,
                offering?.Currency ?? "CAD",
                student is null ? "" : $"{student.FirstName} {student.LastName}".Trim(),
                parentName);
        }).ToList();

        return Task.FromResult<IReadOnlyList<StudentSubscriptionDto>>(result);
    }

    public async Task CancelAsync(string parentUserId, Guid subscriptionId, CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == parentUserId)
            ?? throw new InvalidOperationException("Profil parent introuvable.");

        var childIds = _db.StudentsForAnyTenant
            .Where(s => s.ParentProfileId == parent.Id)
            .Select(s => s.Id)
            .ToHashSet();

        var sub = _db.StudentSubscriptionsForAnyTenant.FirstOrDefault(s => s.Id == subscriptionId)
            ?? throw new InvalidOperationException("Abonnement introuvable.");

        if (!childIds.Contains(sub.StudentId))
            throw new InvalidOperationException("Abonnement introuvable.");

        sub.Status = SubscriptionStatus.Cancelled;
        sub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static StudentSubscriptionDto Map(
        Domain.Entities.StudentSubscription s,
        string offeringTitle,
        string? subject,
        decimal price,
        string currency,
        string studentName,
        string? parentName = null) => new(
        s.Id,
        s.TenantId,
        s.StudentId,
        studentName,
        s.OfferingId,
        offeringTitle,
        subject,
        price,
        currency,
        s.Status.ToString(),
        s.StartDate,
        s.EndDate,
        s.SessionsRemaining,
        parentName);
}
