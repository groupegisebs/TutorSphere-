using TutorSphere.Application.DTOs.StudentSubscriptions;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IStudentSubscriptionService
{
    Task<StudentSubscriptionDto> EnrollAsync(string parentUserId, EnrollStudentRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<StudentSubscriptionDto>> GetForParentUserAsync(string parentUserId, CancellationToken ct = default);
    Task<IReadOnlyList<StudentSubscriptionDto>> GetForCurrentTenantAsync(CancellationToken ct = default);
    Task CancelAsync(string parentUserId, Guid subscriptionId, CancellationToken ct = default);
    Task<StudentSubscriptionDto> AcceptAsync(Guid subscriptionId, CancellationToken ct = default);
    Task<StudentSubscriptionDto> RejectAsync(Guid subscriptionId, CancellationToken ct = default);
}

public class StudentSubscriptionService : IStudentSubscriptionService
{
    private readonly Common.Interfaces.IApplicationDbContext _db;
    private readonly ISubscriptionLessonScheduler _lessonScheduler;

    public StudentSubscriptionService(
        Common.Interfaces.IApplicationDbContext db,
        ISubscriptionLessonScheduler lessonScheduler)
    {
        _db = db;
        _lessonScheduler = lessonScheduler;
    }

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
            && (s.Status == SubscriptionStatus.Pending
                || s.Status == SubscriptionStatus.AwaitingPayment
                || s.Status == SubscriptionStatus.Active));
        if (duplicate)
            throw new InvalidOperationException("Cet enfant est déjà abonné (ou en cours d'abonnement) à cette offre.");

        var activeCount = _db.StudentSubscriptionsForAnyTenant.Count(s =>
            s.OfferingId == offering.Id
            && (s.Status == SubscriptionStatus.Pending
                || s.Status == SubscriptionStatus.AwaitingPayment
                || s.Status == SubscriptionStatus.Active));
        if (activeCount >= offering.MaxCapacity)
            throw new InvalidOperationException(
                $"Cette offre est complète ({offering.MaxCapacity} place(s) maximum).");

        var now = DateTime.UtcNow;
        var endDate = now.AddDays(Math.Max(1, offering.DurationDays));
        StudentScheduleConflictChecker.EnsureNoOfferingConflict(
            _db,
            student.Id,
            offering.Id,
            offering.Conditions,
            now,
            endDate);

        // Ensure the child is linked to the tutor school for subsequent lessons.
        if (student.TenantId != offering.TenantId)
        {
            student.TenantId = offering.TenantId;
            student.UpdatedAt = DateTime.UtcNow;
        }

        var subscription = new Domain.Entities.StudentSubscription
        {
            TenantId = offering.TenantId,
            StudentId = student.Id,
            OfferingId = offering.Id,
            Status = SubscriptionStatus.Pending,
            StartDate = now,
            EndDate = endDate,
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

        if (sub.Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Rejected or SubscriptionStatus.Expired)
            throw new InvalidOperationException("Cet abonnement ne peut plus être annulé.");

        sub.Status = SubscriptionStatus.Cancelled;
        sub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<StudentSubscriptionDto> AcceptAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        var sub = _db.StudentSubscriptions.FirstOrDefault(s => s.Id == subscriptionId)
            ?? throw new InvalidOperationException("Demande d'inscription introuvable.");

        if (sub.Status != SubscriptionStatus.Pending)
            throw new InvalidOperationException("Seules les demandes en attente peuvent être acceptées.");

        var offering = _db.SubscriptionOfferings.FirstOrDefault(o => o.Id == sub.OfferingId)
            ?? throw new InvalidOperationException("Offre introuvable.");

        var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.Id == sub.StudentId);
        var parentName = ResolveParentName(student);

        if (offering.Price <= 0)
        {
            sub.Status = SubscriptionStatus.Active;
            sub.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await _lessonScheduler.EnsureScheduledAsync(sub.Id, ct);
        }
        else
        {
            sub.Status = SubscriptionStatus.AwaitingPayment;
            sub.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return Map(
            sub,
            offering.Title,
            offering.Subject,
            offering.Price,
            offering.Currency,
            student is null ? "" : $"{student.FirstName} {student.LastName}".Trim(),
            parentName);
    }

    public async Task<StudentSubscriptionDto> RejectAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        var sub = _db.StudentSubscriptions.FirstOrDefault(s => s.Id == subscriptionId)
            ?? throw new InvalidOperationException("Demande d'inscription introuvable.");

        if (sub.Status != SubscriptionStatus.Pending)
            throw new InvalidOperationException("Seules les demandes en attente peuvent être refusées.");

        var offering = _db.SubscriptionOfferings.FirstOrDefault(o => o.Id == sub.OfferingId);
        var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.Id == sub.StudentId);
        var parentName = ResolveParentName(student);

        sub.Status = SubscriptionStatus.Rejected;
        sub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Map(
            sub,
            offering?.Title ?? "Offre",
            offering?.Subject,
            offering?.Price ?? 0,
            offering?.Currency ?? "CAD",
            student is null ? "" : $"{student.FirstName} {student.LastName}".Trim(),
            parentName);
    }

    private string? ResolveParentName(Domain.Entities.Student? student)
    {
        if (student?.ParentProfileId is not Guid pid)
            return null;

        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.Id == pid);
        return parent is null ? null : $"{parent.FirstName} {parent.LastName}".Trim();
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
