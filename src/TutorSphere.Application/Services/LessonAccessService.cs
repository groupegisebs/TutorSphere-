using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Lessons;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface ILessonAccessService
{
    bool CanAttendLesson(Guid studentId, Guid lessonId);
    bool HasUsablePack(Guid studentId, Guid tenantId);
    void EnsureStudentEligibleForManualLesson(Guid studentId, Guid tenantId);
    LessonAccessDto Evaluate(Guid studentId, Guid lessonId);
    LessonAccessDto EvaluateForUser(string userId, Guid lessonId);
}

public sealed class LessonAccessService : ILessonAccessService
{
    private readonly IApplicationDbContext _db;
    private readonly IAppUrlProvider _urls;

    public LessonAccessService(IApplicationDbContext db, IAppUrlProvider urls)
    {
        _db = db;
        _urls = urls;
    }

    public LessonAccessDto EvaluateForUser(string userId, Guid lessonId)
    {
        var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.UserId == userId && s.IsActive);
        if (student is null)
            return Denied("/student/subscriptions");

        return Evaluate(student.Id, lessonId);
    }

    public LessonAccessDto Evaluate(Guid studentId, Guid lessonId)
    {
        if (CanAttendLesson(studentId, lessonId))
            return new LessonAccessDto(true, false, null);

        var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.Id == studentId);
        var payPath = student is { IsAutonomous: true } && !string.IsNullOrWhiteSpace(student.UserId)
            ? "/student/subscriptions"
            : "/parent/subscriptions";
        return Denied(payPath);
    }

    public bool CanAttendLesson(Guid studentId, Guid lessonId)
    {
        var lesson = _db.LessonsForAnyTenant.FirstOrDefault(l => l.Id == lessonId);
        if (lesson is null)
            return false;

        if (lesson.SettlementStatus is LessonSettlementStatus.CancelledFree
            or LessonSettlementStatus.TutorNoShow
            or LessonSettlementStatus.LiabilityResolved)
            return false;

        var enrolled = _db.LessonAttendancesForAnyTenant
            .Any(a => a.LessonId == lessonId && a.StudentId == studentId);
        if (!enrolled)
            return false;

        if (lesson.SessionCounted)
            return true;

        return HasEntitlement(studentId, lesson);
    }

    public bool HasUsablePack(Guid studentId, Guid tenantId)
    {
        var now = DateTime.UtcNow;
        return _db.StudentSubscriptionsForAnyTenant.Any(s =>
            s.StudentId == studentId
            && s.TenantId == tenantId
            && s.Status == SubscriptionStatus.Active
            && s.EndDate >= now
            && s.SessionsRemaining > 0);
    }

    public void EnsureStudentEligibleForManualLesson(Guid studentId, Guid tenantId)
    {
        if (HasUsablePack(studentId, tenantId))
            return;

        throw new InvalidOperationException(
            "Cet élève n'a pas de forfait actif (impayé, expiré ou plus de séances). " +
            "Le parent doit payer ou renouveler le pack avant d'être ajouté au cours.");
    }

    private bool HasEntitlement(Guid studentId, Domain.Entities.Lesson lesson)
    {
        var now = DateTime.UtcNow;
        var subs = _db.StudentSubscriptionsForAnyTenant
            .Where(s => s.StudentId == studentId && s.TenantId == lesson.TenantId)
            .ToList();

        foreach (var sub in subs)
        {
            if (sub.Status != SubscriptionStatus.Active || sub.EndDate < now)
                continue;

            if (!string.IsNullOrWhiteSpace(lesson.SessionNotes)
                && lesson.SessionNotes.Contains(SubscriptionLessonScheduler.MarkerFor(sub.Id), StringComparison.OrdinalIgnoreCase))
                return true;

            if (sub.SessionsRemaining > 0)
                return true;
        }

        return false;
    }

    private LessonAccessDto Denied(string path) =>
        new(false, true, $"{_urls.WebBaseUrl.TrimEnd('/')}{path}");
}
