using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Homework;
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
    Task<IReadOnlyList<TutorParentDto>> GetForCurrentTenantAsync(CancellationToken ct = default);
    Task<IReadOnlyList<StudentDto>> GetChildrenForCurrentTenantAsync(Guid parentId, CancellationToken ct = default);
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
    Task<ParentHomeworkBoardDto> GetHomeworkBoardForUserAsync(string userId, CancellationToken ct = default);
    Task<ParentHomeworkDetailDto?> GetHomeworkDetailForUserAsync(
        string userId,
        Guid homeworkId,
        CancellationToken ct = default);
    Task RemindHomeworkForUserAsync(string userId, Guid homeworkId, CancellationToken ct = default);
    Task<ParentProgressDto> GetProgressForUserAsync(
        string userId,
        Guid? childId,
        string? period,
        CancellationToken ct = default);
    Task<(byte[] Content, string FileName)?> BuildProgressReportPdfForUserAsync(
        string userId,
        Guid childId,
        string? period,
        CancellationToken ct = default);
}

public class ParentService : IParentService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILessonAccessService _access;

    public ParentService(IApplicationDbContext db, ITenantContext tenantContext, ILessonAccessService access)
    {
        _db = db;
        _tenantContext = tenantContext;
        _access = access;
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

    public Task<IReadOnlyList<TutorParentDto>> GetForCurrentTenantAsync(CancellationToken ct = default)
    {
        var students = LoadLinkedStudents()
            .Where(s => s.ParentProfileId.HasValue)
            .ToList();
        if (students.Count == 0)
            return Task.FromResult<IReadOnlyList<TutorParentDto>>([]);

        var parentIds = students.Select(s => s.ParentProfileId!.Value).Distinct().ToList();
        var parents = _db.ParentProfilesForAnyTenant
            .Where(p => parentIds.Contains(p.Id))
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .ToList();

        var result = parents
            .Select(p => new TutorParentDto(
                p.Id,
                p.FirstName,
                p.LastName,
                students.Count(s => s.ParentProfileId == p.Id),
                students
                    .Where(s => s.ParentProfileId == p.Id)
                    .OrderBy(s => s.FirstName)
                    .Select(s => $"{s.FirstName} {s.LastName}".Trim())
                    .ToList(),
                string.IsNullOrWhiteSpace(p.UserId) ? null : p.UserId))
            .ToList();

        return Task.FromResult<IReadOnlyList<TutorParentDto>>(result);
    }

    public Task<IReadOnlyList<StudentDto>> GetChildrenForCurrentTenantAsync(Guid parentId, CancellationToken ct = default)
    {
        var children = LoadLinkedStudents()
            .Where(s => s.ParentProfileId == parentId)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(MapStudentToDto)
            .ToList();
        return Task.FromResult<IReadOnlyList<StudentDto>>(children);
    }

    /// <summary>
    /// Élèves rattachés au locataire courant : ceux de son espace et ceux abonnés à ses offres.
    /// </summary>
    private List<Student> LoadLinkedStudents()
    {
        var studentIds = _db.Students.Select(s => s.Id).ToHashSet();
        foreach (var id in _db.StudentSubscriptions.Select(s => s.StudentId).Distinct().ToList())
            studentIds.Add(id);

        if (studentIds.Count == 0)
            return [];

        return _db.StudentsForAnyTenant
            .Where(s => studentIds.Contains(s.Id))
            .ToList();
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
        var pending = SummarizePendingPayments(parent.Id);
        return Task.FromResult<ParentDto?>(MapToDto(parent, count, unread, pending.Count, pending.Amount, pending.Currency));
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
        var pending = SummarizePendingPayments(parent.Id);
        return MapToDto(parent, count, unread, pending.Count, pending.Amount, pending.Currency);
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

        foreach (var child in _db.Students.Where(s => s.ParentProfileId == id).ToList())
            child.ParentProfileId = null;

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
        var pending = SummarizePendingPayments(parent.Id);
        var parentDto = MapToDto(parent, children.Count, unread, pending.Count, pending.Amount, pending.Currency);

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
                BuildEmptyWeekCalendar(),
                pending.Count,
                pending.Amount,
                pending.Currency));
        }

        var attendances = _db.LessonAttendances
            .Where(a => childIds.Contains(a.StudentId))
            .ToList();

        var lessonIds = attendances.Select(a => a.LessonId).Distinct().ToHashSet();
        var lessons = _db.Lessons
            .Where(l => lessonIds.Contains(l.Id))
            .OrderBy(l => l.StartTime)
            .ToList();

        var tenantIds = lessons.Select(l => l.TenantId)
            .Concat(lessons.Where(l => l.DeliveredByTenantId.HasValue).Select(l => l.DeliveredByTenantId!.Value))
            .Distinct()
            .ToList();
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
            weekCalendar,
            pending.Count,
            pending.Amount,
            pending.Currency));
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
            .Concat(lessons.Where(l => l.DeliveredByTenantId.HasValue).Select(l => l.DeliveredByTenantId!.Value))
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

            tenants.TryGetValue(lesson.DeliveredByTenantId ?? lesson.TenantId, out var tenant);
            var teacherId = string.IsNullOrWhiteSpace(tenant?.OwnerUserId) || tenant!.OwnerUserId == userId
                ? null
                : tenant.OwnerUserId;
            var teacherName = string.IsNullOrWhiteSpace(tenant?.Name) ? "Enseignant" : tenant.Name;
            var status = ResolveLessonStatus(lesson, attendance, now);
            var canAttend = _access.CanAttendLesson(student.Id, lesson.Id);

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
                canAttend ? lesson.MeetingUrl : null,
                lesson.Title,
                CanJoinLive: canAttend && !string.IsNullOrWhiteSpace(lesson.MeetingUrl),
                PaymentRequired: !canAttend));
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

    public Task<ParentHomeworkBoardDto> GetHomeworkBoardForUserAsync(string userId, CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId);
        if (parent is null)
            return Task.FromResult(new ParentHomeworkBoardDto([], [], []));

        var children = _db.StudentsForAnyTenant
            .Where(s => s.ParentProfileId == parent.Id)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToList();
        if (children.Count == 0)
            return Task.FromResult(new ParentHomeworkBoardDto([], [], []));

        var childIds = children.Select(c => c.Id).ToList();
        var childLookup = children.ToDictionary(c => c.Id);
        var homeworks = _db.HomeworksForAnyTenant
            .Where(h => childIds.Contains(h.StudentId) && !h.IsDraft)
            .OrderBy(h => h.DueDate ?? DateTime.MaxValue)
            .ToList();

        var tenantIds = homeworks.Select(h => h.TenantId).Distinct().ToList();
        var lessonsById = homeworks
            .Where(h => h.LessonId.HasValue)
            .Select(h => h.LessonId!.Value)
            .Distinct()
            .ToList();
        var lessons = lessonsById.Count == 0
            ? new Dictionary<Guid, Lesson>()
            : _db.LessonsForAnyTenant.Where(l => lessonsById.Contains(l.Id)).ToDictionary(l => l.Id);
        foreach (var lesson in lessons.Values)
            tenantIds.Add(lesson.TenantId);

        var tenants = tenantIds.Count == 0
            ? new Dictionary<Guid, Tenant>()
            : _db.Tenants.Where(t => tenantIds.Contains(t.Id)).ToDictionary(t => t.Id);

        var now = DateTime.UtcNow;
        var items = new List<ParentHomeworkItemDto>();
        foreach (var homework in homeworks)
        {
            if (!childLookup.TryGetValue(homework.StudentId, out var student))
                continue;
            var (teacherName, teacherId) = ResolveHomeworkTeacher(userId, homework, lessons, tenants);
            var content = HomeworkService.MapPublic(homework).Content;
            var submission = HomeworkJson.TryParseSubmission(homework.SubmissionNotes);
            var files = content.Count(b => b.Type is "file" or "link" && !string.IsNullOrWhiteSpace(b.Url))
                        + (submission?.Attachments.Count ?? 0);
            items.Add(new ParentHomeworkItemDto(
                homework.Id,
                student.Id,
                student.FirstName,
                homework.Title,
                homework.Subject,
                teacherName,
                teacherId,
                homework.DueDate,
                ResolveParentHomeworkStatus(homework, now),
                files,
                CanRemindHomework(homework, student)));
        }

        var childDtos = children.Select(child =>
        {
            var own = homeworks.Where(h => h.StudentId == child.Id).ToList();
            return new ParentHomeworkChildDto(
                child.Id,
                child.FirstName,
                child.LastName,
                child.PhotoUrl,
                child.SchoolLevel,
                ComputeOnTimePercent(own, now));
        }).ToList();

        var results = homeworks
            .Where(h => h.IsGraded && h.Grade.HasValue && childLookup.ContainsKey(h.StudentId))
            .OrderByDescending(h => h.UpdatedAt ?? h.CreatedAt)
            .Take(5)
            .Select(h => new ParentHomeworkResultDto(
                h.Id,
                h.StudentId,
                childLookup[h.StudentId].FirstName,
                h.Title,
                h.Grade!.Value,
                h.Feedback,
                h.UpdatedAt ?? h.CreatedAt))
            .ToList();

        return Task.FromResult(new ParentHomeworkBoardDto(childDtos, items, results));
    }

    public Task<ParentHomeworkDetailDto?> GetHomeworkDetailForUserAsync(
        string userId,
        Guid homeworkId,
        CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId);
        if (parent is null)
            return Task.FromResult<ParentHomeworkDetailDto?>(null);

        var homework = _db.HomeworksForAnyTenant.FirstOrDefault(h => h.Id == homeworkId && !h.IsDraft);
        if (homework is null)
            return Task.FromResult<ParentHomeworkDetailDto?>(null);

        var student = _db.StudentsForAnyTenant
            .FirstOrDefault(s => s.Id == homework.StudentId && s.ParentProfileId == parent.Id);
        if (student is null)
            return Task.FromResult<ParentHomeworkDetailDto?>(null);

        Dictionary<Guid, Lesson> lessons = [];
        if (homework.LessonId is Guid lessonId)
        {
            var lesson = _db.LessonsForAnyTenant.FirstOrDefault(l => l.Id == lessonId);
            if (lesson is not null)
                lessons[lesson.Id] = lesson;
        }

        var tenantIds = new List<Guid> { homework.TenantId };
        tenantIds.AddRange(lessons.Values.Select(l => l.TenantId));
        var tenants = _db.Tenants.Where(t => tenantIds.Contains(t.Id)).ToDictionary(t => t.Id);
        var (teacherName, teacherId) = ResolveHomeworkTeacher(userId, homework, lessons, tenants);
        var mapped = HomeworkService.MapPublic(homework);
        var submission = HomeworkJson.TryParseSubmission(homework.SubmissionNotes);
        var attachments = submission?.Attachments ?? [];
        var missingIds = attachments.Where(a => string.IsNullOrWhiteSpace(a.Url) && a.DocumentId != Guid.Empty)
            .Select(a => a.DocumentId)
            .ToList();
        var docs = missingIds.Count == 0
            ? []
            : _db.DocumentsForAnyTenant.Where(d => missingIds.Contains(d.Id)).ToList();

        var files = attachments.Select(a =>
        {
            var url = a.Url;
            if (string.IsNullOrWhiteSpace(url))
                url = docs.FirstOrDefault(d => d.Id == a.DocumentId)?.FileUrl;
            return new ParentHomeworkFileDto(a.FileName, url);
        }).ToList();

        return Task.FromResult<ParentHomeworkDetailDto?>(new ParentHomeworkDetailDto(
            homework.Id,
            student.Id,
            student.FirstName,
            homework.Title,
            homework.Subject,
            homework.Description,
            homework.Instructions,
            mapped.Content.Select(b => new ParentHomeworkBlockDto(b.Type, b.Title, b.Body, b.Url)).ToList(),
            homework.DueDate,
            ResolveParentHomeworkStatus(homework, DateTime.UtcNow),
            teacherName,
            teacherId,
            homework.SubmittedAt,
            submission?.Text,
            files,
            homework.Grade,
            homework.Feedback,
            homework.IsGraded,
            CanRemindHomework(homework, student)));
    }

    public async Task RemindHomeworkForUserAsync(string userId, Guid homeworkId, CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId)
            ?? throw new InvalidOperationException("Profil parent introuvable.");
        var homework = _db.HomeworksForAnyTenant.FirstOrDefault(h => h.Id == homeworkId && !h.IsDraft)
            ?? throw new InvalidOperationException("Devoir introuvable.");
        var student = _db.StudentsForAnyTenant
            .FirstOrDefault(s => s.Id == homework.StudentId && s.ParentProfileId == parent.Id)
            ?? throw new InvalidOperationException("Enfant introuvable.");

        if (!CanRemindHomework(homework, student))
            throw new InvalidOperationException(
                "Impossible d'envoyer un rappel : activez l'accès espace Élève, ou le devoir est déjà remis.");

        var due = homework.DueDate.HasValue
            ? $" pour le {homework.DueDate.Value.ToLocalTime():d}"
            : "";
        _db.Add(new Message
        {
            TenantId = student.TenantId,
            SenderUserId = userId,
            RecipientUserId = student.UserId!,
            Subject = $"Rappel devoir : {homework.Title}",
            Body =
                $"Rappel : le devoir « {homework.Title} » est à remettre{due}. " +
                "Connecte-toi à l'espace Élève pour le réaliser. Ton parent ne peut pas le remettre à ta place."
        });
        await _db.SaveChangesAsync(ct);
    }

    public Task<ParentProgressDto> GetProgressForUserAsync(
        string userId,
        Guid? childId,
        string? period,
        CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId);
        if (parent is null)
            return Task.FromResult(new ParentProgressDto([], null));

        var children = _db.StudentsForAnyTenant
            .Where(s => s.ParentProfileId == parent.Id)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToList();
        var childDtos = children
            .Select(s => new ParentProgressChildDto(s.Id, s.FirstName, s.LastName, s.SchoolLevel, s.PhotoUrl))
            .ToList();
        if (children.Count == 0)
            return Task.FromResult(new ParentProgressDto(childDtos, null));

        var student = childId.HasValue
            ? children.FirstOrDefault(s => s.Id == childId.Value) ?? children[0]
            : children[0];

        var report = BuildProgressReport(userId, parent.Id, student, period);
        return Task.FromResult(new ParentProgressDto(childDtos, report));
    }

    public async Task<(byte[] Content, string FileName)?> BuildProgressReportPdfForUserAsync(
        string userId,
        Guid childId,
        string? period,
        CancellationToken ct = default)
    {
        var dto = await GetProgressForUserAsync(userId, childId, period, ct);
        if (dto.Report is null)
            return null;

        var r = dto.Report;
        var lines = new List<string>
        {
            "TutorSphere — Rapport de progression",
            "",
            $"{r.FirstName} {r.LastName}".Trim(),
            string.IsNullOrWhiteSpace(r.SchoolLevel) ? "" : r.SchoolLevel,
            $"Periode : {DescribePeriod(period)}",
            $"Edite le {DateTime.Now:dd/MM/yyyy}",
            "",
            $"Progression generale : {(r.ProgressPercent.HasValue ? r.ProgressPercent + " %" : "—")}",
            $"Moyenne : {(r.AverageGrade.HasValue ? r.AverageGrade.Value.ToString("0.0") + " / 20" : "—")}",
            $"Assiduite : {(r.AttendancePercent.HasValue ? r.AttendancePercent + " %" : "—")}",
            $"Competences acquises : {r.SkillsAcquired}/{r.SkillsTotal}",
            r.HasGroupBenchmark ? "Comparaison : moyenne de reference anonymisee (meme niveau)." : "Comparaison : resultats anterieurs de l'enfant uniquement.",
            "",
            "Progression par matiere"
        };
        foreach (var s in r.Subjects)
            lines.Add($"- {s.Subject} : {(s.Percent.HasValue ? s.Percent + " % (" + BandLabel(s.Band) + ")" : "—")}");

        lines.Add("");
        lines.Add("Observations");
        if (r.Observations.Count == 0)
            lines.Add("- Aucune observation sur la periode.");
        foreach (var o in r.Observations.Take(8))
            lines.Add($"- {o.CreatedAt:dd/MM} {o.TeacherName} : {o.Text}");

        lines.Add("");
        lines.Add("Points d'attention");
        if (r.Attention.Count == 0)
            lines.Add("- Aucun point critique.");
        foreach (var a in r.Attention)
            lines.Add($"- {a.Title} — {a.Recommendation}");

        lines.Add("");
        lines.Add($"Objectifs : {r.GoalsAchieved} sur {r.GoalsTotal} atteints");
        foreach (var g in r.Goals)
            lines.Add($"- [{(g.Achieved ? "x" : " ")}] {g.Title}");

        lines.Add("");
        lines.Add("Document genere pour le parent. Aucune comparaison nominative entre enfants.");

        var safeName = $"{r.FirstName}-{r.LastName}".Trim().Replace(' ', '-');
        return (InvoicePdfGenerator.FromTextLines(lines), $"progression-{safeName}-{DateTime.Today:yyyyMMdd}.pdf");
    }

    private ParentProgressReportDto BuildProgressReport(
        string userId,
        Guid parentId,
        Student student,
        string? period)
    {
        var (startUtc, endUtc, prevStartUtc, prevEndUtc) = ResolveProgressWindow(period);

        var attendances = _db.LessonAttendancesForAnyTenant.Where(a => a.StudentId == student.Id).ToList();
        var lessonIds = attendances.Select(a => a.LessonId).Distinct().ToList();
        var lessons = lessonIds.Count == 0
            ? []
            : _db.LessonsForAnyTenant
                .Where(l => lessonIds.Contains(l.Id) && l.SettlementStatus != LessonSettlementStatus.CancelledFree)
                .ToList();

        var homeworks = _db.HomeworksForAnyTenant
            .Where(h => h.StudentId == student.Id && !h.IsDraft)
            .ToList();
        var reports = _db.LessonReportsForAnyTenant.Where(r => r.StudentId == student.Id).ToList();

        int? AttendanceFor(DateTime from, DateTime to)
        {
            var monthLessons = lessons.Where(l => l.StartTime >= from && l.StartTime < to).ToList();
            if (monthLessons.Count == 0) return null;
            var present = monthLessons.Count(l => attendances.Any(a => a.LessonId == l.Id && a.IsPresent));
            return (int)Math.Round(present * 100.0 / monthLessons.Count);
        }

        List<decimal> GradesIn(DateTime from, DateTime to) =>
            homeworks.Where(h => h.IsGraded && h.Grade.HasValue && Timestamp(h) >= from && Timestamp(h) < to)
                .Select(h => h.Grade!.Value)
                .ToList();

        var currentGrades = GradesIn(startUtc, endUtc);
        var prevGrades = GradesIn(prevStartUtc, prevEndUtc);
        var average = currentGrades.Count > 0 ? Math.Round(currentGrades.Average(), 1) : (decimal?)null;
        var prevAverage = prevGrades.Count > 0 ? Math.Round(prevGrades.Average(), 1) : (decimal?)null;
        var progress = ToProgressPercent(currentGrades);
        var prevProgress = ToProgressPercent(prevGrades);
        var attendance = AttendanceFor(startUtc, endUtc);
        var prevAttendance = AttendanceFor(prevStartUtc, prevEndUtc);

        var tenantIds = homeworks.Select(h => h.TenantId).Concat(lessons.Select(l => l.TenantId)).Distinct().ToList();
        var tenants = tenantIds.Count == 0
            ? new Dictionary<Guid, Tenant>()
            : _db.Tenants
                .Where(t => tenantIds.Contains(t.Id) && t.Slug != "platform-parents")
                .ToDictionary(t => t.Id);

        var familyIds = _db.StudentsForAnyTenant
            .Where(s => s.ParentProfileId == parentId)
            .Select(s => s.Id)
            .ToHashSet();

        var parentReports = reports.Where(r => r.SentToParent || r.SentAt.HasValue).ToList();
        var timeline = BuildTimeline(student, familyIds, startUtc, endUtc, tenantIds);
        var subjects = BuildSubjects(homeworks, student, startUtc, endUtc);
        var subjectsPrev = BuildSubjects(homeworks, student, prevStartUtc, prevEndUtc);
        var skillsNow = BuildSkills(homeworks, parentReports, subjects, startUtc, endUtc);
        var skillsPrev = BuildSkills(homeworks, parentReports, subjectsPrev, prevStartUtc, prevEndUtc);
        var acquiredNow = skillsNow.Count(s => s.Status == "acquired");
        var acquiredPrev = skillsPrev.Count(s => s.Status == "acquired");

        var observations = BuildObservations(userId, parentReports, lessons, tenants, startUtc, endUtc);
        var attention = subjects
            .Where(s => s.Band == "reinforce")
            .Select(s => new ParentProgressAttentionDto(
                $"{s.Subject}",
                "Planifier 15 minutes de revision, 3 fois par semaine."))
            .Concat(skillsNow.Where(s => s.Status == "reinforce")
                .Select(s => new ParentProgressAttentionDto(
                    s.Name,
                    "Revenir sur cette competence avec des exercices courts et reguliers.")))
            .GroupBy(a => a.Title, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(5)
            .ToList();

        var goals = BuildGoals(student, subjects);

        return new ParentProgressReportDto(
            student.Id,
            student.FirstName,
            student.LastName,
            student.SchoolLevel,
            progress,
            progress.HasValue && prevProgress.HasValue ? progress.Value - prevProgress.Value : null,
            average,
            average.HasValue && prevAverage.HasValue ? Math.Round(average.Value - prevAverage.Value, 1) : null,
            attendance,
            attendance.HasValue && prevAttendance.HasValue ? attendance.Value - prevAttendance.Value : null,
            acquiredNow,
            skillsNow.Count,
            acquiredNow - acquiredPrev,
            timeline.Any(p => p.GroupAveragePercent.HasValue),
            timeline,
            subjects,
            skillsNow,
            observations,
            attention,
            goals.Count(g => g.Achieved),
            goals.Count,
            goals);
    }

    private List<ParentProgressPointDto> BuildTimeline(
        Student student,
        HashSet<Guid> familyIds,
        DateTime startUtc,
        DateTime endUtc,
        List<Guid> tenantIds)
    {
        var points = new List<ParentProgressPointDto>();
        var cursor = new DateTime(startUtc.Year, startUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        if (cursor < startUtc)
            cursor = new DateTime(startUtc.ToLocalTime().Year, startUtc.ToLocalTime().Month, 1, 0, 0, 0, DateTimeKind.Local).ToUniversalTime();

        var own = _db.HomeworksForAnyTenant
            .Where(h => h.StudentId == student.Id && h.IsGraded && h.Grade.HasValue && !h.IsDraft)
            .ToList();

        List<Homework>? peers = null;
        if (!string.IsNullOrWhiteSpace(student.SchoolLevel) && tenantIds.Count > 0)
        {
            var level = student.SchoolLevel;
            var peerStudents = _db.StudentsForAnyTenant
                .Where(s => s.SchoolLevel == level && !familyIds.Contains(s.Id))
                .Select(s => s.Id)
                .ToList();
            if (peerStudents.Count >= 3)
            {
                peers = _db.HomeworksForAnyTenant
                    .Where(h => peerStudents.Contains(h.StudentId)
                                && tenantIds.Contains(h.TenantId)
                                && h.IsGraded && h.Grade.HasValue && !h.IsDraft)
                    .ToList();
                var distinctPeers = peers.Select(h => h.StudentId).Distinct().Count();
                if (distinctPeers < 3)
                    peers = null;
            }
        }

        for (var month = cursor; month < endUtc; month = month.AddMonths(1))
        {
            var next = month.AddMonths(1);
            var grades = own.Where(h => Timestamp(h) >= month && Timestamp(h) < next).Select(h => h.Grade!.Value).ToList();
            if (grades.Count == 0)
                continue;
            var pct = ToProgressPercent(grades);
            if (!pct.HasValue)
                continue;

            int? group = null;
            if (peers is not null)
            {
                var g = peers.Where(h => Timestamp(h) >= month && Timestamp(h) < next).Select(h => h.Grade!.Value).ToList();
                if (g.Count >= 3)
                    group = ToProgressPercent(g);
            }

            points.Add(new ParentProgressPointDto(month, pct.Value, group));
        }

        return points;
    }

    private List<ParentProgressSubjectDto> BuildSubjects(
        IReadOnlyList<Homework> homeworks,
        Student student,
        DateTime startUtc,
        DateTime endUtc)
    {
        var graded = homeworks.Where(h => h.IsGraded && h.Grade.HasValue && Timestamp(h) >= startUtc && Timestamp(h) < endUtc).ToList();
        var map = graded
            .GroupBy(h => string.IsNullOrWhiteSpace(h.Subject) ? "Autre" : h.Subject.Trim())
            .Select(g =>
            {
                var pct = ToProgressPercent(g.Select(x => x.Grade!.Value).ToList());
                return new ParentProgressSubjectDto(g.Key, pct, BandFor(pct));
            })
            .OrderBy(s => s.Subject)
            .ToList();

        if (map.Count == 0)
        {
            map = ParseSubjects(student.Subjects)
                .Select(s => new ParentProgressSubjectDto(s, null, "progress"))
                .ToList();
        }

        return map;
    }

    private static List<ParentProgressSkillDto> BuildSkills(
        IReadOnlyList<Homework> homeworks,
        IReadOnlyList<LessonReport> reports,
        IReadOnlyList<ParentProgressSubjectDto> subjects,
        DateTime startUtc,
        DateTime endUtc)
    {
        var skills = new Dictionary<string, ParentProgressSkillDto>(StringComparer.OrdinalIgnoreCase);

        void Upsert(string name, string? subject, string status, int? percent)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;
            var key = name.Trim();
            if (skills.TryGetValue(key, out var existing) && Rank(existing.Status) <= Rank(status))
                return;
            skills[key] = new ParentProgressSkillDto(key, subject, status, percent);
        }

        foreach (var h in homeworks.Where(h => Timestamp(h) >= startUtc && Timestamp(h) < endUtc))
        {
            var mapped = HomeworkService.MapPublic(h);
            foreach (var c in mapped.Criteria)
            {
                string status;
                int? pct = null;
                if (h.IsGraded && h.Grade.HasValue)
                {
                    pct = (int)Math.Round(h.Grade.Value / 20m * 100m);
                    status = BandToSkill(BandFor(pct));
                }
                else
                    status = "progress";
                Upsert(c.Name, h.Subject, status, pct);
            }
        }

        foreach (var report in reports.Where(r => r.CreatedAt >= startUtc && r.CreatedAt < endUtc))
        {
            foreach (var s in SplitBits(report.Strengths))
                Upsert(s, null, "acquired", 100);
            foreach (var s in SplitBits(report.Weaknesses))
                Upsert(s, null, "reinforce", null);
        }

        if (skills.Count == 0)
        {
            foreach (var sub in subjects.Where(s => s.Percent.HasValue))
                Upsert(sub.Subject, sub.Subject, BandToSkill(sub.Band), sub.Percent);
        }

        return skills.Values.OrderBy(s => Rank(s.Status)).ThenBy(s => s.Name).ToList();
    }

    private List<ParentProgressObservationDto> BuildObservations(
        string parentUserId,
        IReadOnlyList<LessonReport> reports,
        IReadOnlyList<Lesson> lessons,
        IReadOnlyDictionary<Guid, Tenant> tenants,
        DateTime startUtc,
        DateTime endUtc)
    {
        var lessonMap = lessons.ToDictionary(l => l.Id);
        var list = new List<ParentProgressObservationDto>();
        foreach (var report in reports.Where(r => r.CreatedAt >= startUtc && r.CreatedAt < endUtc)
                     .OrderByDescending(r => r.CreatedAt)
                     .Take(8))
        {
            lessonMap.TryGetValue(report.LessonId, out var lesson);
            tenants.TryGetValue(lesson?.TenantId ?? report.TenantId, out var tenant);
            var teacherId = string.IsNullOrWhiteSpace(tenant?.OwnerUserId) || tenant!.OwnerUserId == parentUserId
                ? null
                : tenant.OwnerUserId;
            var teacherName = teacherId is null
                ? (string.IsNullOrWhiteSpace(tenant?.Name) ? "Enseignant" : tenant.Name)
                : ResolveUserDisplayName(teacherId);
            if (string.IsNullOrWhiteSpace(teacherName) || teacherName == "Utilisateur")
                teacherName = string.IsNullOrWhiteSpace(tenant?.Name) ? "Enseignant" : tenant.Name;
            var bits = new[] { report.Observations, report.Strengths, report.Weaknesses }
                .Where(s => !string.IsNullOrWhiteSpace(s));
            var text = string.Join(" ", bits);
            if (string.IsNullOrWhiteSpace(text))
                continue;
            list.Add(new ParentProgressObservationDto(
                report.Id,
                report.CreatedAt,
                teacherName,
                teacherId,
                lesson?.Subject,
                text.Trim()));
        }

        return list;
    }

    private static List<ParentProgressGoalDto> BuildGoals(
        Student student,
        IReadOnlyList<ParentProgressSubjectDto> subjects)
    {
        var raw = ParseLines(student.Goals);
        if (raw.Count == 0)
        {
            return subjects.Select(s => new ParentProgressGoalDto(
                    $"Progresser en {s.Subject}",
                    s.Percent >= 80))
                .Take(5)
                .ToList();
        }

        return raw.Select(goal =>
        {
            var match = subjects.FirstOrDefault(s =>
                goal.Contains(s.Subject, StringComparison.OrdinalIgnoreCase));
            return new ParentProgressGoalDto(goal, match?.Percent >= 80);
        }).ToList();
    }

    private static (DateTime Start, DateTime End, DateTime PrevStart, DateTime PrevEnd) ResolveProgressWindow(string? period)
    {
        var now = DateTime.UtcNow;
        var local = now.ToLocalTime();
        DateTime startLocal;
        DateTime endLocal = local;
        DateTime prevStart;
        DateTime prevEnd;

        switch ((period ?? "term").ToLowerInvariant())
        {
            case "month":
                startLocal = new DateTime(local.Year, local.Month, 1);
                prevStart = startLocal.AddMonths(-1);
                prevEnd = startLocal;
                break;
            case "year":
                startLocal = local.Month >= 9
                    ? new DateTime(local.Year, 9, 1)
                    : new DateTime(local.Year - 1, 9, 1);
                prevStart = startLocal.AddYears(-1);
                prevEnd = startLocal;
                break;
            case "all":
                startLocal = new DateTime(local.AddMonths(-11).Year, local.AddMonths(-11).Month, 1);
                prevStart = startLocal.AddMonths(-12);
                prevEnd = startLocal;
                break;
            default:
                if (local.Month >= 9)
                {
                    startLocal = new DateTime(local.Year, 9, 1);
                    prevStart = new DateTime(local.Year, 4, 1);
                    prevEnd = new DateTime(local.Year, 7, 1);
                }
                else if (local.Month <= 3)
                {
                    startLocal = new DateTime(local.Year, 1, 1);
                    prevStart = new DateTime(local.Year - 1, 9, 1);
                    prevEnd = startLocal;
                }
                else
                {
                    startLocal = new DateTime(local.Year, 4, 1);
                    prevStart = new DateTime(local.Year, 1, 1);
                    prevEnd = startLocal;
                }
                break;
        }

        static DateTime Utc(DateTime localDt) =>
            DateTime.SpecifyKind(localDt, DateTimeKind.Local).ToUniversalTime();

        return (Utc(startLocal), Utc(endLocal.Date.AddDays(1)), Utc(prevStart), Utc(prevEnd));
    }

    private static DateTime Timestamp(Homework h) => h.UpdatedAt ?? h.CreatedAt;

    private static string BandFor(int? percent) =>
        percent is null ? "progress" : percent >= 80 ? "verygood" : percent >= 70 ? "progress" : "reinforce";

    private static string BandToSkill(string band) => band == "verygood" ? "acquired" : band == "reinforce" ? "reinforce" : "progress";

    private static int Rank(string status) => status switch
    {
        "acquired" => 0,
        "progress" => 1,
        _ => 2
    };

    private static string BandLabel(string band) => band switch
    {
        "verygood" => "Tres bien",
        "reinforce" => "A renforcer",
        _ => "En progres"
    };

    private static string DescribePeriod(string? period) => (period ?? "term") switch
    {
        "month" => "ce mois",
        "year" => "cette annee",
        "all" => "12 derniers mois",
        _ => "ce trimestre"
    };

    private static IReadOnlyList<string> ParseLines(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];
        return value.Split(new[] { '\n', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IEnumerable<string> SplitBits(string? value) => ParseLines(value);

    private static bool CanRemindHomework(Homework homework, Student student) =>
        !homework.IsGraded
        && !homework.SubmittedAt.HasValue
        && !string.IsNullOrWhiteSpace(student.UserId);

    private static int? ComputeOnTimePercent(IReadOnlyList<Homework> homeworks, DateTime nowUtc)
    {
        var relevant = homeworks
            .Where(h => h.DueDate.HasValue && (h.SubmittedAt.HasValue || h.DueDate < nowUtc))
            .ToList();
        if (relevant.Count == 0)
            return null;

        var onTime = relevant.Count(h =>
            h.SubmittedAt.HasValue && h.DueDate.HasValue && h.SubmittedAt.Value <= h.DueDate.Value);
        return (int)Math.Round(onTime * 100.0 / relevant.Count);
    }

    private static string ResolveParentHomeworkStatus(Homework homework, DateTime nowUtc)
    {
        if (homework.IsGraded)
            return "done";
        if (homework.SubmittedAt.HasValue)
            return "grading";
        if (homework.DueDate is DateTime due)
        {
            var dueLocal = due.Kind == DateTimeKind.Utc ? due.ToLocalTime() : DateTime.SpecifyKind(due, DateTimeKind.Local);
            var today = DateTime.Now.Date;
            if (dueLocal.Date < today)
                return "overdue";
            if (dueLocal.Date <= today.AddDays(1))
                return "urgent";
        }

        return "todo";
    }

    private static (string Name, string? UserId) ResolveHomeworkTeacher(
        string parentUserId,
        Homework homework,
        IReadOnlyDictionary<Guid, Lesson> lessons,
        IReadOnlyDictionary<Guid, Tenant> tenants)
    {
        Guid tenantId = homework.TenantId;
        if (homework.LessonId is Guid lessonId && lessons.TryGetValue(lessonId, out var lesson))
            tenantId = lesson.TenantId;

        if (!tenants.TryGetValue(tenantId, out var tenant) || string.IsNullOrWhiteSpace(tenant.Name))
            return ("Enseignant", null);

        var teacherId = string.IsNullOrWhiteSpace(tenant.OwnerUserId) || tenant.OwnerUserId == parentUserId
            ? null
            : tenant.OwnerUserId;
        return (tenant.Name, teacherId);
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

        var tenantIds = lessons.Select(l => l.TenantId)
            .Concat(lessons.Where(l => l.DeliveredByTenantId.HasValue).Select(l => l.DeliveredByTenantId!.Value))
            .Distinct()
            .ToHashSet();
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
            tenants.TryGetValue(nextLessonEntity.DeliveredByTenantId ?? nextLessonEntity.TenantId, out var nextTenant);
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

    private static ParentDto MapToDto(
        ParentProfile p,
        int childrenCount,
        int unreadMessagesCount = 0,
        int pendingPaymentsCount = 0,
        decimal pendingPaymentsAmount = 0,
        string? pendingPaymentsCurrency = null) => new(
        p.Id,
        p.FirstName,
        p.LastName,
        p.Email,
        p.Phone,
        childrenCount,
        unreadMessagesCount,
        p.Country,
        pendingPaymentsCount,
        pendingPaymentsAmount,
        pendingPaymentsCurrency);

    private (int Count, decimal Amount, string? Currency) SummarizePendingPayments(Guid parentId)
    {
        var childIds = _db.StudentsForAnyTenant
            .Where(s => s.ParentProfileId == parentId)
            .Select(s => s.Id)
            .ToList();
        if (childIds.Count == 0)
            return (0, 0, null);

        var subscriptionIds = _db.StudentSubscriptionsForAnyTenant
            .Where(s => childIds.Contains(s.StudentId))
            .Select(s => s.Id)
            .ToList();
        if (subscriptionIds.Count == 0)
            return (0, 0, null);

        var pending = _db.PaymentsForAnyTenant
            .Where(p => p.SubscriptionId.HasValue
                        && subscriptionIds.Contains(p.SubscriptionId.Value)
                        && p.Status == PaymentStatus.Pending)
            .Select(p => new { p.Amount, p.Currency })
            .ToList();

        if (pending.Count == 0)
            return (0, 0, null);

        return (pending.Count, pending.Sum(p => p.Amount), pending[0].Currency);
    }

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
        tenants.TryGetValue(lesson.DeliveredByTenantId ?? lesson.TenantId, out var tenant);
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
