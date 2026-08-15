using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Lessons;
using TutorSphere.Application.DTOs.Messages;
using TutorSphere.Application.DTOs.Parents;
using TutorSphere.Application.DTOs.Payments;
using TutorSphere.Application.DTOs.Students;
using TutorSphere.Domain.Common;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IParentService
{
    Task<IReadOnlyList<ParentDto>> GetAllAsync(CancellationToken ct = default);
    Task<ParentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ParentDto?> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<ParentDto> CreateAsync(CreateParentRequest request, CancellationToken ct = default);
    Task<ParentDto> UpdateAsync(Guid id, UpdateParentRequest request, CancellationToken ct = default);
    Task<ParentDto> UpdateForUserAsync(string userId, UpdateParentRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StudentDto>> GetChildrenAsync(Guid parentId, CancellationToken ct = default);
    Task<IReadOnlyList<StudentDto>> GetChildrenForUserAsync(string userId, CancellationToken ct = default);
    Task<StudentDto> AddChildForUserAsync(string userId, ParentAddChildRequest request, CancellationToken ct = default);
    Task<StudentDto> UpdateChildForUserAsync(string userId, Guid childId, ParentUpdateChildRequest request, CancellationToken ct = default);
    Task DeleteChildForUserAsync(string userId, Guid childId, CancellationToken ct = default);
    Task<ParentDashboardDto?> GetDashboardForUserAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<LessonDto>> GetLessonsForUserAsync(
        string userId,
        DateTime start,
        DateTime end,
        CancellationToken ct = default);
    Task<IReadOnlyList<ParentPaymentDto>> GetPaymentsForUserAsync(
        string userId,
        CancellationToken ct = default);
    Task<IReadOnlyList<ConversationDto>> GetTeacherContactsForUserAsync(
        string userId,
        CancellationToken ct = default);
    Task<ParentChildFollowUpDto?> GetChildFollowUpForUserAsync(
        string userId,
        Guid childId,
        string? period,
        CancellationToken ct = default);
    Task<ParentCalendarDto> GetCalendarForUserAsync(
        string userId,
        DateTime start,
        DateTime end,
        CancellationToken ct = default);
}

public class ParentService : IParentService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ParentService(IApplicationDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public Task<IReadOnlyList<ParentDto>> GetAllAsync(CancellationToken ct = default)
    {
        var parents = _db.ParentProfiles
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .ToList()
            .Select(p => MapToDto(p, _db.Students.Count(s => s.ParentProfileId == p.Id)))
            .ToList();
        return Task.FromResult<IReadOnlyList<ParentDto>>(parents);
    }

    public Task<ParentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var parent = _db.ParentProfiles.FirstOrDefault(p => p.Id == id);
        if (parent is null) return Task.FromResult<ParentDto?>(null);
        var count = _db.Students.Count(s => s.ParentProfileId == id);
        return Task.FromResult<ParentDto?>(MapToDto(parent, count));
    }

    public Task<ParentDto?> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId);
        if (parent is null) return Task.FromResult<ParentDto?>(null);
        var count = _db.StudentsForAnyTenant.Count(s => s.ParentProfileId == parent.Id);
        var unread = _db.Messages.Count(m => m.RecipientUserId == userId && !m.IsRead);
        return Task.FromResult<ParentDto?>(MapToDto(parent, count, unread));
    }

    public async Task<ParentDto> CreateAsync(CreateParentRequest request, CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var parent = new ParentProfile
        {
            TenantId = tenantId,
            UserId = string.Empty,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone?.Trim(),
            Country = NormalizeParentCountry(request.Country)
        };

        _db.Add(parent);
        await _db.SaveChangesAsync(ct);
        return MapToDto(parent, 0);
    }

    public async Task<ParentDto> UpdateAsync(Guid id, UpdateParentRequest request, CancellationToken ct = default)
    {
        var parent = _db.ParentProfiles.FirstOrDefault(p => p.Id == id)
            ?? throw new InvalidOperationException("Parent introuvable.");

        ApplyParentUpdate(parent, request);

        await _db.SaveChangesAsync(ct);
        var count = _db.Students.Count(s => s.ParentProfileId == id);
        return MapToDto(parent, count);
    }

    public async Task<ParentDto> UpdateForUserAsync(string userId, UpdateParentRequest request, CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId)
            ?? throw new InvalidOperationException("Parent introuvable.");

        ApplyParentUpdate(parent, request);
        await _db.SaveChangesAsync(ct);
        var count = _db.StudentsForAnyTenant.Count(s => s.ParentProfileId == parent.Id);
        var unread = _db.Messages.Count(m => m.RecipientUserId == userId && !m.IsRead);
        return MapToDto(parent, count, unread);
    }

    private static void ApplyParentUpdate(ParentProfile parent, UpdateParentRequest request)
    {
        parent.FirstName = request.FirstName.Trim();
        parent.LastName = request.LastName.Trim();
        parent.Email = request.Email.Trim();
        parent.Phone = request.Phone?.Trim();
        parent.Country = NormalizeParentCountry(request.Country);
        parent.UpdatedAt = DateTime.UtcNow;
    }

    private static string? NormalizeParentCountry(string? country)
    {
        var code = ProfileVisibility.NormalizeCode(country);
        return code.Length == 2 ? code : null;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var parent = _db.ParentProfiles.FirstOrDefault(p => p.Id == id)
            ?? throw new InvalidOperationException("Parent introuvable.");

        _db.Remove(parent);
        await _db.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<StudentDto>> GetChildrenAsync(Guid parentId, CancellationToken ct = default)
    {
        var children = _db.StudentsForAnyTenant
            .Where(s => s.ParentProfileId == parentId)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToList()
            .Select(MapStudentToDto)
            .ToList();
        return Task.FromResult<IReadOnlyList<StudentDto>>(children);
    }

    public async Task<IReadOnlyList<StudentDto>> GetChildrenForUserAsync(string userId, CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId);
        if (parent is null)
            return [];

        return await GetChildrenAsync(parent.Id, ct);
    }

    public async Task<StudentDto> AddChildForUserAsync(string userId, ParentAddChildRequest request, CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId)
            ?? throw new InvalidOperationException("Profil parent introuvable. Déconnectez-vous puis reconnectez-vous, ou contactez le support.");

        return await AddChildForParentAsync(parent, request, ct);
    }

    public async Task<StudentDto> UpdateChildForUserAsync(string userId, Guid childId, ParentUpdateChildRequest request, CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId)
            ?? throw new InvalidOperationException("Profil parent introuvable. Déconnectez-vous puis reconnectez-vous, ou contactez le support.");

        var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.Id == childId && s.ParentProfileId == parent.Id)
            ?? throw new InvalidOperationException("Enfant introuvable.");

        ValidateChildNames(request.FirstName, request.LastName);
        var dateOfBirth = NormalizeDateOfBirth(request.DateOfBirth);
        var email = NormalizeEmail(request.Email);

        if (email is not null &&
            _db.StudentsForAnyTenant.Any(s => s.Id != childId && s.Email != null && s.Email.ToLower() == email.ToLower()))
            throw new InvalidOperationException("Cette adresse courriel est déjà utilisée par un autre élève.");

        student.FirstName = request.FirstName.Trim();
        student.LastName = request.LastName.Trim();
        student.Email = email;
        student.DateOfBirth = dateOfBirth;
        student.SchoolLevel = request.SchoolLevel?.Trim();
        student.SchoolName = request.SchoolName?.Trim();
        student.Subjects = request.Subjects?.Trim();
        student.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapStudentToDto(student);
    }

    public async Task DeleteChildForUserAsync(string userId, Guid childId, CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId)
            ?? throw new InvalidOperationException("Profil parent introuvable. Déconnectez-vous puis reconnectez-vous, ou contactez le support.");

        var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.Id == childId && s.ParentProfileId == parent.Id)
            ?? throw new InvalidOperationException("Enfant introuvable.");

        _db.Remove(student);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<StudentDto> AddChildForParentAsync(ParentProfile parent, ParentAddChildRequest request, CancellationToken ct)
    {
        ValidateChildNames(request.FirstName, request.LastName);
        var dateOfBirth = NormalizeDateOfBirth(request.DateOfBirth);
        var email = NormalizeEmail(request.Email);

        if (email is not null &&
            _db.StudentsForAnyTenant.Any(s => s.Email != null && s.Email.ToLower() == email.ToLower()))
            throw new InvalidOperationException("Cette adresse courriel est déjà utilisée par un autre élève.");

        var student = new Student
        {
            TenantId = parent.TenantId,
            ParentProfileId = parent.Id,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            DateOfBirth = dateOfBirth,
            SchoolLevel = request.SchoolLevel?.Trim(),
            SchoolName = request.SchoolName?.Trim(),
            Subjects = request.Subjects?.Trim(),
            IsActive = true
        };

        _db.Add(student);
        await _db.SaveChangesAsync(ct);
        return MapStudentToDto(student);
    }

    private static void ValidateChildNames(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            throw new InvalidOperationException("Le prénom et le nom sont obligatoires.");
    }

    private static DateTime? NormalizeDateOfBirth(DateTime? dateOfBirth)
    {
        if (!dateOfBirth.HasValue)
            return null;

        var dob = dateOfBirth.Value.Date;
        if (dob > DateTime.UtcNow.Date)
            throw new InvalidOperationException("La date de naissance ne peut pas être dans le futur.");

        return DateTime.SpecifyKind(dob, DateTimeKind.Utc);
    }

    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim();

    public Task<ParentDashboardDto?> GetDashboardForUserAsync(string userId, CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId);
        if (parent is null)
            return Task.FromResult<ParentDashboardDto?>(null);

        var children = _db.StudentsForAnyTenant
            .Where(s => s.ParentProfileId == parent.Id)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToList();

        var childIds = children.Select(c => c.Id).ToList();
        var unread = _db.Messages.Count(m => m.RecipientUserId == userId && !m.IsRead);
        var parentDto = MapToDto(parent, children.Count, unread);

        if (childIds.Count == 0)
        {
            return Task.FromResult<ParentDashboardDto?>(new ParentDashboardDto(
                parentDto,
                null,
                0,
                null,
                null,
                [],
                [],
                [],
                [],
                [],
                null,
                [],
                BuildEmptyWeekCalendar()));
        }

        var attendances = _db.LessonAttendances
            .Where(a => childIds.Contains(a.StudentId))
            .ToList();

        var lessonIds = attendances.Select(a => a.LessonId).Distinct().ToHashSet();
        var lessons = _db.Lessons
            .Where(l => lessonIds.Contains(l.Id))
            .OrderBy(l => l.StartTime)
            .ToList();

        var tenantIds = lessons.Select(l => l.TenantId).Distinct().ToList();
        var tenants = _db.Tenants
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionary(t => t.Id);

        var now = DateTime.UtcNow;
        var today = DateTime.Today;
        var lessonsToday = lessons.Count(l => l.StartTime.ToLocalTime().Date == today);
        var nextLesson = lessons.FirstOrDefault(l => l.StartTime >= now);

        var gradedHomework = _db.Homeworks
            .Where(h => childIds.Contains(h.StudentId) && h.IsGraded && h.Grade.HasValue)
            .ToList();

        decimal? averageGrade = gradedHomework.Count > 0
            ? Math.Round(gradedHomework.Average(h => h.Grade!.Value), 1)
            : null;

        var childDtos = children
            .Select(s => MapDashboardChild(s, lessons, attendances, gradedHomework))
            .ToList();

        var upcomingSessions = lessons
            .Where(l => l.StartTime >= now)
            .Take(5)
            .Select(l => MapDashboardSession(l, tenants))
            .ToList();

        var childNameLookup = children.ToDictionary(c => c.Id, c => $"{c.FirstName} {c.LastName}".Trim());

        var pendingHomework = _db.Homeworks
            .Where(h => childIds.Contains(h.StudentId) && !h.IsGraded && !h.SubmittedAt.HasValue)
            .OrderBy(h => h.DueDate ?? DateTime.MaxValue)
            .Take(5)
            .ToList()
            .Select(h => new ParentDashboardHomeworkDto(
                h.Id,
                h.Title,
                childNameLookup.GetValueOrDefault(h.StudentId, "—"),
                h.DueDate,
                h.SubmittedAt.HasValue,
                h.IsGraded))
            .ToList();

        var recentReports = _db.LessonReports
            .Where(r => childIds.Contains(r.StudentId))
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .ToList()
            .Select(r =>
            {
                var lesson = lessons.FirstOrDefault(l => l.Id == r.LessonId);
                tenants.TryGetValue(lesson?.TenantId ?? Guid.Empty, out var tenant);
                return new ParentDashboardReportDto(
                    r.Id,
                    tenant?.Name ?? "—",
                    lesson?.Subject,
                    r.TopicsStudied,
                    r.CreatedAt,
                    childNameLookup.GetValueOrDefault(r.StudentId, "—"));
            })
            .ToList();

        var recentMessageEntities = _db.Messages
            .Where(m => m.RecipientUserId == userId || m.SenderUserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(5)
            .ToList();

        var recentMessages = recentMessageEntities
            .Select(m => new ParentDashboardMessageDto(
                m.Id,
                ResolveUserDisplayName(m.SenderUserId == userId ? m.RecipientUserId : m.SenderUserId),
                TruncatePreview(m.Body),
                m.RecipientUserId == userId && !m.IsRead,
                m.CreatedAt))
            .ToList();

        var subscriptionIds = _db.StudentSubscriptions
            .Where(ss => childIds.Contains(ss.StudentId))
            .Select(ss => ss.Id)
            .ToList();

        var recentPayment = _db.Payments
            .Where(p => p.SubscriptionId.HasValue
                        && subscriptionIds.Contains(p.SubscriptionId.Value)
                        && p.Status == PaymentStatus.Completed)
            .OrderByDescending(p => p.CompletedAt ?? p.CreatedAt)
            .Select(p => new ParentDashboardPaymentDto(
                p.Id,
                p.Amount,
                p.Currency,
                p.Status.ToString(),
                p.CompletedAt))
            .FirstOrDefault();

        var recentDocuments = _db.Documents
            .Where(d => d.StudentId.HasValue && childIds.Contains(d.StudentId.Value))
            .OrderByDescending(d => d.CreatedAt)
            .Take(5)
            .Select(d => new ParentDashboardDocumentDto(
                d.Id,
                d.Name,
                d.FileSizeBytes,
                d.ContentType,
                d.FileUrl,
                d.CreatedAt))
            .ToList();

        var activeSubEntity = _db.StudentSubscriptions
            .Where(ss => childIds.Contains(ss.StudentId) && ss.Status == SubscriptionStatus.Active)
            .OrderByDescending(ss => ss.StartDate)
            .FirstOrDefault();

        ParentDashboardSubscriptionDto? activeSubscription = null;
        if (activeSubEntity is not null)
        {
            var offering = _db.SubscriptionOfferings.FirstOrDefault(o => o.Id == activeSubEntity.OfferingId);
            activeSubscription = new ParentDashboardSubscriptionDto(
                activeSubEntity.Id,
                offering?.Title ?? "—",
                activeSubEntity.Status.ToString(),
                activeSubEntity.EndDate);
        }

        var weekCalendar = BuildWeekCalendar(lessons, children, attendances);

        return Task.FromResult<ParentDashboardDto?>(new ParentDashboardDto(
            parentDto,
            averageGrade,
            lessonsToday,
            nextLesson?.StartTime,
            activeSubscription,
            childDtos,
            upcomingSessions,
            pendingHomework,
            recentReports,
            recentMessages,
            recentPayment,
            recentDocuments,
            weekCalendar));
    }

    public Task<IReadOnlyList<LessonDto>> GetLessonsForUserAsync(
        string userId,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        if (end <= start)
            throw new InvalidOperationException("La date de fin doit être postérieure à la date de début.");

        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId);
        if (parent is null)
            return Task.FromResult<IReadOnlyList<LessonDto>>([]);

        var childIds = _db.StudentsForAnyTenant
            .Where(s => s.ParentProfileId == parent.Id)
            .Select(s => s.Id)
            .ToList();
        if (childIds.Count == 0)
            return Task.FromResult<IReadOnlyList<LessonDto>>([]);

        var lessonIds = _db.LessonAttendancesForAnyTenant
            .Where(a => childIds.Contains(a.StudentId))
            .Select(a => a.LessonId)
            .Distinct()
            .ToList();

        var lessons = _db.LessonsForAnyTenant
            .Where(l => lessonIds.Contains(l.Id)
                        && l.SettlementStatus != LessonSettlementStatus.CancelledFree
                        && l.StartTime < end && l.EndTime > start)
            .OrderBy(l => l.StartTime)
            .ToList()
            .Select(l => new LessonDto(
                l.Id,
                l.Title,
                l.Description,
                l.Subject,
                l.StartTime,
                l.EndTime,
                l.Mode.ToString(),
                l.Location,
                l.MeetingUrl,
                l.SessionNotes,
                l.CreatedAt,
                l.UpdatedAt,
                l.SettlementStatus.ToString(),
                l.CancelledAt,
                l.SessionCounted,
                l.TutorLiable,
                l.TutorLiabilityResolution))
            .ToList();

        return Task.FromResult<IReadOnlyList<LessonDto>>(lessons);
    }

    public Task<ParentCalendarDto> GetCalendarForUserAsync(
        string userId,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        if (end <= start)
            throw new InvalidOperationException("La date de fin doit être postérieure à la date de début.");

        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId);
        if (parent is null)
            return Task.FromResult(new ParentCalendarDto([], []));

        var children = _db.StudentsForAnyTenant
            .Where(s => s.ParentProfileId == parent.Id)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToList();
        if (children.Count == 0)
            return Task.FromResult(new ParentCalendarDto([], []));

        var childDtos = children
            .Select(s => new ParentCalendarChildDto(s.Id, s.FirstName, s.LastName, s.SchoolLevel))
            .ToList();
        var childIds = children.Select(s => s.Id).ToList();
        var childLookup = children.ToDictionary(s => s.Id);

        var attendances = _db.LessonAttendancesForAnyTenant
            .Where(a => childIds.Contains(a.StudentId))
            .ToList();
        var lessonIds = attendances.Select(a => a.LessonId).Distinct().ToList();
        var lessons = lessonIds.Count == 0
            ? []
            : _db.LessonsForAnyTenant
                .Where(l => lessonIds.Contains(l.Id)
                            && l.SettlementStatus != LessonSettlementStatus.CancelledFree
                            && l.StartTime < end && l.EndTime > start)
                .ToList();
        var lessonMap = lessons.ToDictionary(l => l.Id);

        var homeworks = _db.HomeworksForAnyTenant
            .Where(h => childIds.Contains(h.StudentId) && !h.IsDraft && h.DueDate.HasValue)
            .ToList()
            .Where(h =>
            {
                var due = h.DueDate!.Value;
                return due < end && due >= start.AddDays(-1);
            })
            .ToList();

        var tenantIds = lessons.Select(l => l.TenantId)
            .Concat(homeworks.Select(h => h.TenantId))
            .Distinct()
            .ToList();
        var tenants = tenantIds.Count == 0
            ? new Dictionary<Guid, Tenant>()
            : _db.Tenants
                .Where(t => tenantIds.Contains(t.Id) && t.Slug != "platform-parents")
                .ToDictionary(t => t.Id);

        var now = DateTime.UtcNow;
        var events = new List<ParentCalendarEventDto>();

        foreach (var attendance in attendances)
        {
            if (!lessonMap.TryGetValue(attendance.LessonId, out var lesson))
                continue;
            if (!childLookup.TryGetValue(attendance.StudentId, out var student))
                continue;

            tenants.TryGetValue(lesson.TenantId, out var tenant);
            var teacherId = string.IsNullOrWhiteSpace(tenant?.OwnerUserId) || tenant!.OwnerUserId == userId
                ? null
                : tenant.OwnerUserId;
            var teacherName = string.IsNullOrWhiteSpace(tenant?.Name) ? "Enseignant" : tenant.Name;
            var status = ResolveLessonStatus(lesson, attendance, now);

            events.Add(new ParentCalendarEventDto(
                attendance.Id,
                "lesson",
                student.Id,
                student.FirstName,
                lesson.Subject ?? lesson.Title,
                lesson.StartTime,
                lesson.EndTime,
                teacherName,
                teacherId,
                status,
                lesson.MeetingUrl,
                lesson.Title));
        }

        foreach (var homework in homeworks)
        {
            if (!childLookup.TryGetValue(homework.StudentId, out var student))
                continue;

            var (startLocal, endLocal) = ResolveHomeworkSlot(homework.DueDate!.Value);
            if (endLocal < start || startLocal >= end)
                continue;

            Tenant? tenant = null;
            if (homework.LessonId is Guid linkedLessonId && lessonMap.TryGetValue(linkedLessonId, out var linkedLesson))
                tenants.TryGetValue(linkedLesson.TenantId, out tenant);
            else
                tenants.TryGetValue(homework.TenantId, out tenant);

            var teacherId = string.IsNullOrWhiteSpace(tenant?.OwnerUserId) || tenant!.OwnerUserId == userId
                ? null
                : tenant.OwnerUserId;
            var teacherName = string.IsNullOrWhiteSpace(tenant?.Name) ? "Enseignant" : tenant.Name;
            var isEval = homework.IsGraded;
            var status = isEval
                ? "graded"
                : homework.SubmittedAt.HasValue
                    ? "submitted"
                    : homework.DueDate < now ? "overdue" : "due";

            events.Add(new ParentCalendarEventDto(
                homework.Id,
                isEval ? "evaluation" : "homework",
                student.Id,
                student.FirstName,
                homework.Subject ?? homework.Title,
                startLocal.ToUniversalTime(),
                endLocal.ToUniversalTime(),
                teacherName,
                teacherId,
                status,
                null,
                homework.Title));
        }

        return Task.FromResult(new ParentCalendarDto(
            childDtos,
            events.OrderBy(e => e.StartTime).ToList()));
    }

    private static string ResolveLessonStatus(Lesson lesson, LessonAttendance attendance, DateTime now)
    {
        if (lesson.CancelledAt.HasValue || lesson.SettlementStatus == LessonSettlementStatus.CancelledFree)
            return "cancelled";
        if (now < lesson.StartTime)
            return "upcoming";
        if (now <= lesson.EndTime)
            return "live";
        if (attendance.IsPresent)
            return "present";
        return "done";
    }

    private static (DateTime Start, DateTime End) ResolveHomeworkSlot(DateTime due)
    {
        var local = due.Kind == DateTimeKind.Utc ? due.ToLocalTime() : DateTime.SpecifyKind(due, DateTimeKind.Local);
        var start = local.TimeOfDay == TimeSpan.Zero ? local.Date.AddHours(17) : local;
        return (DateTime.SpecifyKind(start, DateTimeKind.Local), DateTime.SpecifyKind(start.AddHours(1), DateTimeKind.Local));
    }

    public async Task<IReadOnlyList<ParentPaymentDto>> GetPaymentsForUserAsync(
        string userId,
        CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId);
        if (parent is null)
            return [];

        var childIds = _db.StudentsForAnyTenant
            .Where(s => s.ParentProfileId == parent.Id)
            .Select(s => s.Id)
            .ToList();
        if (childIds.Count == 0)
            return [];

        var subscriptionIds = _db.StudentSubscriptionsForAnyTenant
            .Where(s => childIds.Contains(s.StudentId))
            .Select(s => s.Id)
            .ToList();
        if (subscriptionIds.Count == 0)
            return [];

        var payments = _db.PaymentsForAnyTenant
            .Where(p => p.SubscriptionId.HasValue && subscriptionIds.Contains(p.SubscriptionId.Value))
            .OrderByDescending(p => p.CreatedAt)
            .ToList();

        // Ensure invoices for completed payments (backfill)
        foreach (var payment in payments.Where(p =>
                     p.Status == PaymentStatus.Completed && !p.InvoiceId.HasValue))
        {
            try
            {
                await EnsureInvoiceInlineAsync(payment, parent.Id, ct);
            }
            catch
            {
                // keep listing even if invoice creation fails
            }
        }

        // reload after backfill
        payments = _db.PaymentsForAnyTenant
            .Where(p => p.SubscriptionId.HasValue && subscriptionIds.Contains(p.SubscriptionId.Value))
            .OrderByDescending(p => p.CreatedAt)
            .ToList();

        var subs = _db.StudentSubscriptionsForAnyTenant
            .Where(s => subscriptionIds.Contains(s.Id))
            .ToDictionary(s => s.Id);
        var offeringIds = subs.Values.Select(s => s.OfferingId).Distinct().ToList();
        var offerings = _db.SubscriptionOfferingsForAnyTenant
            .Where(o => offeringIds.Contains(o.Id))
            .ToDictionary(o => o.Id);
        var students = _db.StudentsForAnyTenant
            .Where(s => childIds.Contains(s.Id))
            .ToDictionary(s => s.Id);
        var invoiceIds = payments.Where(p => p.InvoiceId.HasValue).Select(p => p.InvoiceId!.Value).Distinct().ToList();
        var invoices = _db.InvoicesForAnyTenant
            .Where(i => invoiceIds.Contains(i.Id))
            .ToDictionary(i => i.Id);
        var tenantIds = payments.Select(p => p.TenantId).Distinct().ToList();
        var tenants = _db.Tenants.Where(t => tenantIds.Contains(t.Id)).ToDictionary(t => t.Id);

        return payments.Select(p =>
        {
            string? studentName = null;
            string? description = "Paiement abonnement";
            if (p.SubscriptionId is Guid sid && subs.TryGetValue(sid, out var sub))
            {
                if (students.TryGetValue(sub.StudentId, out var student))
                    studentName = $"{student.FirstName} {student.LastName}".Trim();
                if (offerings.TryGetValue(sub.OfferingId, out var offering))
                    description = offering.Title;
            }

            invoices.TryGetValue(p.InvoiceId ?? Guid.Empty, out var invoice);
            tenants.TryGetValue(p.TenantId, out var tutor);

            return new ParentPaymentDto(
                p.Id,
                p.InvoiceId,
                invoice?.InvoiceNumber,
                description,
                studentName,
                tutor?.Name,
                p.Amount,
                p.Currency,
                p.Status.ToString(),
                p.CreatedAt,
                p.CompletedAt,
                p.Status is PaymentStatus.Completed or PaymentStatus.Pending);
        }).ToList();
    }

    public Task<IReadOnlyList<ConversationDto>> GetTeacherContactsForUserAsync(
        string userId,
        CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId);
        if (parent is null)
            return Task.FromResult<IReadOnlyList<ConversationDto>>([]);

        var children = _db.StudentsForAnyTenant
            .Where(s => s.ParentProfileId == parent.Id && s.IsActive)
            .Select(s => new { s.Id, s.TenantId })
            .ToList();

        var tenantIds = new HashSet<Guid>();
        foreach (var child in children)
            tenantIds.Add(child.TenantId);

        var childIds = children.Select(c => c.Id).ToList();
        if (childIds.Count > 0)
        {
            foreach (var tid in _db.StudentSubscriptionsForAnyTenant
                         .Where(s => childIds.Contains(s.StudentId))
                         .Select(s => s.TenantId)
                         .Distinct())
                tenantIds.Add(tid);
        }

        var owners = _db.Tenants
            .Where(t => tenantIds.Contains(t.Id)
                        && t.OwnerUserId != null
                        && t.OwnerUserId != ""
                        && t.Slug != "platform-parents")
            .Select(t => new { t.OwnerUserId, t.Name })
            .ToList();

        var existingPartnerIds = _db.Messages
            .Where(m => m.SenderUserId == userId || m.RecipientUserId == userId)
            .AsEnumerable()
            .Select(m => m.SenderUserId == userId ? m.RecipientUserId : m.SenderUserId)
            .Distinct()
            .ToHashSet();

        var contacts = new List<ConversationDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var owner in owners)
        {
            if (string.IsNullOrWhiteSpace(owner.OwnerUserId) || owner.OwnerUserId == userId)
                continue;
            if (!seen.Add(owner.OwnerUserId))
                continue;

            var last = _db.Messages
                .Where(m =>
                    (m.SenderUserId == userId && m.RecipientUserId == owner.OwnerUserId) ||
                    (m.SenderUserId == owner.OwnerUserId && m.RecipientUserId == userId))
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefault();

            var unread = _db.Messages.Count(m =>
                m.SenderUserId == owner.OwnerUserId && m.RecipientUserId == userId && !m.IsRead);

            contacts.Add(new ConversationDto(
                owner.OwnerUserId,
                string.IsNullOrWhiteSpace(owner.Name) ? "Enseignant" : $"Enseignant — {owner.Name}",
                last is null
                    ? null
                    : new MessageDto(
                        last.Id,
                        last.SenderUserId,
                        last.RecipientUserId,
                        last.Subject,
                        last.Body,
                        last.IsRead,
                        last.ReadAt,
                        last.CreatedAt),
                unread));
        }

        foreach (var partnerId in existingPartnerIds)
        {
            if (partnerId == userId || !seen.Add(partnerId))
                continue;

            var last = _db.Messages
                .Where(m =>
                    (m.SenderUserId == userId && m.RecipientUserId == partnerId) ||
                    (m.SenderUserId == partnerId && m.RecipientUserId == userId))
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefault();

            var unread = _db.Messages.Count(m =>
                m.SenderUserId == partnerId && m.RecipientUserId == userId && !m.IsRead);

            contacts.Add(new ConversationDto(
                partnerId,
                "Contact",
                last is null
                    ? null
                    : new MessageDto(
                        last.Id,
                        last.SenderUserId,
                        last.RecipientUserId,
                        last.Subject,
                        last.Body,
                        last.IsRead,
                        last.ReadAt,
                        last.CreatedAt),
                unread));
        }

        return Task.FromResult<IReadOnlyList<ConversationDto>>(contacts);
    }

    public Task<ParentChildFollowUpDto?> GetChildFollowUpForUserAsync(
        string userId,
        Guid childId,
        string? period,
        CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId);
        if (parent is null)
            return Task.FromResult<ParentChildFollowUpDto?>(null);

        var student = _db.StudentsForAnyTenant
            .FirstOrDefault(s => s.Id == childId && s.ParentProfileId == parent.Id);
        if (student is null)
            return Task.FromResult<ParentChildFollowUpDto?>(null);

        var now = DateTime.UtcNow;
        var localNow = now.ToLocalTime();
        var monthStartLocal = new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, DateTimeKind.Local);
        var monthStartUtc = monthStartLocal.ToUniversalTime();
        var monthEndUtc = monthStartLocal.AddMonths(1).ToUniversalTime();
        var prevMonthStartUtc = monthStartLocal.AddMonths(-1).ToUniversalTime();

        var termStartMonth = ((localNow.Month - 1) / 3) * 3 + 1;
        var termStartUtc = new DateTime(localNow.Year, termStartMonth, 1, 0, 0, 0, DateTimeKind.Local)
            .ToUniversalTime();
        var useAll = string.Equals(period, "all", StringComparison.OrdinalIgnoreCase);
        var subjectWindowStart = useAll ? DateTime.MinValue : termStartUtc;

        var attendances = _db.LessonAttendancesForAnyTenant
            .Where(a => a.StudentId == student.Id)
            .ToList();
        var lessonIds = attendances.Select(a => a.LessonId).Distinct().ToList();
        var lessons = lessonIds.Count == 0
            ? []
            : _db.LessonsForAnyTenant
                .Where(l => lessonIds.Contains(l.Id) && l.SettlementStatus != LessonSettlementStatus.CancelledFree)
                .ToList();

        var homeworks = _db.HomeworksForAnyTenant
            .Where(h => h.StudentId == student.Id && !h.IsDraft)
            .ToList();

        var graded = homeworks.Where(h => h.IsGraded && h.Grade.HasValue).ToList();
        int? progress = ToProgressPercent(graded.Select(h => h.Grade!.Value).ToList());

        var thisMonthGrades = graded
            .Where(h => (h.UpdatedAt ?? h.CreatedAt) >= monthStartUtc && (h.UpdatedAt ?? h.CreatedAt) < monthEndUtc)
            .Select(h => h.Grade!.Value)
            .ToList();
        var lastMonthGrades = graded
            .Where(h => (h.UpdatedAt ?? h.CreatedAt) >= prevMonthStartUtc && (h.UpdatedAt ?? h.CreatedAt) < monthStartUtc)
            .Select(h => h.Grade!.Value)
            .ToList();
        var thisMonthPct = ToProgressPercent(thisMonthGrades);
        var lastMonthPct = ToProgressPercent(lastMonthGrades);
        int? delta = thisMonthPct.HasValue && lastMonthPct.HasValue
            ? thisMonthPct.Value - lastMonthPct.Value
            : null;

        var homeworkDueCount = homeworks.Count(h => !h.IsGraded && !h.SubmittedAt.HasValue);

        var nextLessonEntity = lessons
            .Where(l => l.StartTime >= now)
            .OrderBy(l => l.StartTime)
            .FirstOrDefault();

        var tenantIds = lessons.Select(l => l.TenantId).Distinct().ToHashSet();
        var subscriptions = _db.StudentSubscriptionsForAnyTenant
            .Where(s => s.StudentId == student.Id
                        && s.Status != SubscriptionStatus.Cancelled
                        && s.Status != SubscriptionStatus.Rejected
                        && s.Status != SubscriptionStatus.Expired)
            .ToList();
        foreach (var sub in subscriptions)
            tenantIds.Add(sub.TenantId);

        var tenants = tenantIds.Count == 0
            ? new Dictionary<Guid, Tenant>()
            : _db.Tenants
                .Where(t => tenantIds.Contains(t.Id) && t.Slug != "platform-parents")
                .ToDictionary(t => t.Id);

        ParentChildNextLessonDto? nextLesson = null;
        if (nextLessonEntity is not null)
        {
            tenants.TryGetValue(nextLessonEntity.TenantId, out var nextTenant);
            nextLesson = new ParentChildNextLessonDto(
                nextLessonEntity.Id,
                nextLessonEntity.Subject ?? nextLessonEntity.Title,
                string.IsNullOrWhiteSpace(nextLessonEntity.Description) ? nextLessonEntity.Title : nextLessonEntity.Description,
                nextLessonEntity.StartTime,
                nextLessonEntity.EndTime,
                nextTenant?.Name ?? "Enseignant");
        }

        var monthLessons = lessons
            .Where(l => l.StartTime >= monthStartUtc && l.StartTime < monthEndUtc)
            .ToList();
        int? attendance = null;
        if (monthLessons.Count > 0)
        {
            var present = monthLessons.Count(l =>
                attendances.Any(a => a.LessonId == l.Id && a.IsPresent));
            attendance = (int)Math.Round(present * 100.0 / monthLessons.Count);
        }

        var periodGrades = graded
            .Where(h => (h.UpdatedAt ?? h.CreatedAt) >= subjectWindowStart)
            .ToList();
        var subjectMap = periodGrades
            .GroupBy(h => string.IsNullOrWhiteSpace(h.Subject) ? "Autre" : h.Subject.Trim())
            .Select(g => new ParentChildSubjectProgressDto(
                g.Key,
                ToProgressPercent(g.Select(x => x.Grade!.Value).ToList())))
            .OrderBy(s => s.Subject)
            .ToList();

        if (subjectMap.Count == 0)
        {
            subjectMap = ParseSubjects(student.Subjects)
                .Select(s => new ParentChildSubjectProgressDto(s, null))
                .ToList();
        }

        var recentHomework = homeworks
            .OrderBy(h => h.SubmittedAt.HasValue || h.IsGraded)
            .ThenBy(h => h.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(h => h.CreatedAt)
            .Take(8)
            .Select(h => new ParentChildHomeworkItemDto(
                h.Id,
                h.Title,
                h.Subject,
                h.DueDate,
                h.SubmittedAt.HasValue,
                h.IsGraded,
                h.Grade,
                h.CreatedAt))
            .ToList();

        var offeringIds = subscriptions.Select(s => s.OfferingId).Distinct().ToList();
        var offerings = offeringIds.Count == 0
            ? []
            : _db.SubscriptionOfferingsForAnyTenant
                .Where(o => offeringIds.Contains(o.Id))
                .ToList();
        var offeringByTenant = offerings
            .GroupBy(o => o.TenantId)
            .ToDictionary(g => g.Key, g => g.First());

        var teachers = new List<ParentChildTeacherDto>();
        var seenTeachers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tenant in tenants.Values)
        {
            if (string.IsNullOrWhiteSpace(tenant.OwnerUserId) || tenant.OwnerUserId == userId)
                continue;
            if (!seenTeachers.Add(tenant.OwnerUserId))
                continue;

            offeringByTenant.TryGetValue(tenant.Id, out var offering);
            var lastSubject = lessons
                .Where(l => l.TenantId == tenant.Id)
                .OrderByDescending(l => l.StartTime)
                .Select(l => l.Subject)
                .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

            teachers.Add(new ParentChildTeacherDto(
                tenant.OwnerUserId,
                string.IsNullOrWhiteSpace(tenant.Name) ? "Enseignant" : tenant.Name,
                offering?.Subject ?? offering?.Title ?? lastSubject));
        }

        var documents = _db.DocumentsForAnyTenant
            .Where(d => d.StudentId == student.Id)
            .OrderByDescending(d => d.CreatedAt)
            .Take(10)
            .Select(d => new ParentDashboardDocumentDto(
                d.Id,
                d.Name,
                d.FileSizeBytes,
                d.ContentType,
                d.FileUrl,
                d.CreatedAt))
            .ToList();

        return Task.FromResult<ParentChildFollowUpDto?>(new ParentChildFollowUpDto(
            student.Id,
            progress,
            delta,
            homeworkDueCount,
            nextLesson,
            attendance,
            subjectMap,
            recentHomework,
            teachers,
            documents,
            !string.IsNullOrEmpty(student.UserId)));
    }

    private static int? ToProgressPercent(IReadOnlyList<decimal> grades)
    {
        if (grades.Count == 0)
            return null;

        var average = grades.Average();
        return (int)Math.Round(average / 20m * 100m);
    }

    private async Task EnsureInvoiceInlineAsync(Payment payment, Guid parentProfileId, CancellationToken ct)
    {
        if (payment.InvoiceId.HasValue)
            return;

        string? offeringTitle = null;
        if (payment.SubscriptionId is Guid subId)
        {
            var subscription = _db.StudentSubscriptionsForAnyTenant.FirstOrDefault(s => s.Id == subId);
            if (subscription is not null)
            {
                var offering = _db.SubscriptionOfferingsForAnyTenant.FirstOrDefault(o => o.Id == subscription.OfferingId);
                offeringTitle = offering?.Title;
            }
        }

        var invoice = new Invoice
        {
            TenantId = payment.TenantId,
            ParentProfileId = parentProfileId,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            Amount = payment.Amount,
            Currency = payment.Currency,
            Status = payment.Status,
            IssuedAt = payment.CreatedAt == default ? DateTime.UtcNow : payment.CreatedAt,
            PaidAt = payment.CompletedAt,
            StripeInvoiceId = offeringTitle
        };
        _db.Add(invoice);
        await _db.SaveChangesAsync(ct);
        payment.InvoiceId = invoice.Id;
        payment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private Guid RequireTenantId()
    {
        if (!_tenantContext.HasTenant || _tenantContext.TenantId is null)
            throw new InvalidOperationException("Contexte locataire requis.");
        return _tenantContext.TenantId.Value;
    }

    private static ParentDto MapToDto(ParentProfile p, int childrenCount, int unreadMessagesCount = 0) => new(
        p.Id,
        p.FirstName,
        p.LastName,
        p.Email,
        p.Phone,
        childrenCount,
        unreadMessagesCount,
        p.Country);

    private static StudentDto MapStudentToDto(Student s) => new(
        s.Id,
        s.FirstName,
        s.LastName,
        s.Email,
        s.Phone,
        s.DateOfBirth,
        s.Age,
        s.IsMinor,
        s.IsAutonomous,
        s.ParentProfileId,
        null,
        s.PhotoUrl,
        s.SchoolLevel,
        s.SchoolName,
        ParseSubjects(s.Subjects),
        s.Notes,
        s.IsActive,
        s.CreatedAt,
        !string.IsNullOrEmpty(s.UserId),
        null,
        s.Country);

    private static ParentDashboardChildDto MapDashboardChild(
        Student student,
        IReadOnlyList<Lesson> lessons,
        IReadOnlyList<LessonAttendance> attendances,
        IReadOnlyList<Homework> gradedHomework)
    {
        var studentLessonIds = attendances
            .Where(a => a.StudentId == student.Id)
            .Select(a => a.LessonId)
            .ToHashSet();

        var studentLessons = lessons.Where(l => studentLessonIds.Contains(l.Id)).ToList();
        var now = DateTime.UtcNow;
        var nextLesson = studentLessons.FirstOrDefault(l => l.StartTime >= now);

        var childGrades = gradedHomework
            .Where(h => h.StudentId == student.Id && h.Grade.HasValue)
            .Select(h => h.Grade!.Value)
            .ToList();

        decimal? average = childGrades.Count > 0
            ? Math.Round(childGrades.Average(), 1)
            : null;

        int? progress = average.HasValue
            ? (int)Math.Round(average.Value / 20m * 100m)
            : null;

        return new ParentDashboardChildDto(
            student.Id,
            student.FirstName,
            student.LastName,
            student.PhotoUrl,
            student.SchoolLevel,
            average,
            progress,
            nextLesson?.StartTime,
            nextLesson?.Subject);
    }

    private static ParentDashboardSessionDto MapDashboardSession(
        Lesson lesson,
        IReadOnlyDictionary<Guid, Tenant> tenants)
    {
        tenants.TryGetValue(lesson.TenantId, out var tenant);
        var tutorName = tenant?.Name ?? lesson.Title;
        return new ParentDashboardSessionDto(
            lesson.Id,
            tutorName,
            lesson.Subject,
            lesson.StartTime,
            lesson.Mode.ToString());
    }

    private static IReadOnlyList<ParentDashboardCalendarDayDto> BuildWeekCalendar(
        IReadOnlyList<Lesson> lessons,
        IReadOnlyList<Student> children,
        IReadOnlyList<LessonAttendance> attendances)
    {
        var start = DateTime.Today;
        while (start.DayOfWeek != DayOfWeek.Monday)
            start = start.AddDays(-1);

        if (DateTime.Today.DayOfWeek == DayOfWeek.Sunday)
            start = start.AddDays(-7);

        var childLookup = children.ToDictionary(c => c.Id);
        var eventColors = new[] { "purple", "pink", "green", "orange" };
        var days = new List<ParentDashboardCalendarDayDto>();

        for (var i = 0; i < 5; i++)
        {
            var date = start.AddDays(i);
            var dayLessons = lessons
                .Where(l => l.StartTime.ToLocalTime().Date == date)
                .OrderBy(l => l.StartTime)
                .ToList();

            var events = new List<ParentDashboardCalendarEventDto>();
            foreach (var lesson in dayLessons)
            {
                var studentId = attendances.FirstOrDefault(a => a.LessonId == lesson.Id)?.StudentId;
                var studentName = studentId.HasValue && childLookup.TryGetValue(studentId.Value, out var child)
                    ? child.FirstName
                    : "—";

                events.Add(new ParentDashboardCalendarEventDto(
                    lesson.Subject ?? lesson.Title,
                    studentName,
                    lesson.StartTime.ToLocalTime().ToString("HH:mm"),
                    eventColors[events.Count % eventColors.Length]));
            }

            days.Add(new ParentDashboardCalendarDayDto(
                date,
                date.ToString("ddd dd"),
                date == DateTime.Today,
                events));
        }

        return days;
    }

    private static IReadOnlyList<ParentDashboardCalendarDayDto> BuildEmptyWeekCalendar()
    {
        var start = DateTime.Today;
        while (start.DayOfWeek != DayOfWeek.Monday)
            start = start.AddDays(-1);

        if (DateTime.Today.DayOfWeek == DayOfWeek.Sunday)
            start = start.AddDays(-7);

        return Enumerable.Range(0, 5)
            .Select(i =>
            {
                var date = start.AddDays(i);
                return new ParentDashboardCalendarDayDto(
                    date,
                    date.ToString("ddd dd"),
                    date == DateTime.Today,
                    []);
            })
            .ToList();
    }

    private string ResolveUserDisplayName(string userId)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId);
        if (parent is not null)
            return $"{parent.FirstName} {parent.LastName}".Trim();

        var tenant = _db.Tenants.FirstOrDefault(t => t.OwnerUserId == userId);
        if (tenant is not null)
            return tenant.Name;

        return "Utilisateur";
    }

    private static string TruncatePreview(string body)
    {
        var trimmed = body.Trim();
        return trimmed.Length <= 80 ? trimmed : $"{trimmed[..77]}…";
    }

    private static IReadOnlyList<string> ParseSubjects(string? subjects) =>
        string.IsNullOrWhiteSpace(subjects)
            ? []
            : subjects.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
