using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Calendar;
using TutorSphere.Application.DTOs.Lessons;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace TutorSphere.Application.Services;

public interface ILessonService
{
    Task<IReadOnlyList<LessonDto>> CreateAsync(CreateLessonRequest request, CancellationToken ct = default);
    Task<LessonDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<LessonDto>> GetByDateRangeAsync(DateTime start, DateTime end, CancellationToken ct = default);
    Task<IReadOnlyList<LessonDto>> GetByViewAsync(CalendarView view, DateTime date, CancellationToken ct = default);
    Task<LessonDto> UpdateAsync(Guid id, UpdateLessonRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<LessonDto> CancelAsync(Guid id, CancelLessonRequest request, CancellationToken ct = default);
    Task<LessonDto> MarkTutorNoShowAsync(Guid id, MarkTutorNoShowRequest request, CancellationToken ct = default);
    Task<LessonDto> ResolveTutorLiabilityAsync(Guid id, ResolveTutorLiabilityRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<LessonAttendanceDto>> GetAttendancesAsync(Guid lessonId, CancellationToken ct = default);
    Task<LessonAttendanceDto> SetAttendanceAsync(Guid lessonId, SetAttendanceRequest request, CancellationToken ct = default);
    Task SettleDueLessonsAsync(CancellationToken ct = default);
    /// <summary>Notifie les élèves (et parents mineurs) via SignalR que la salle est ouverte.</summary>
    Task NotifySessionStartedAsync(Guid lessonId, CancellationToken ct = default);
}

public class LessonService : ILessonService
{
    public const int CancelFreeHours = 24;

    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IEmailService _email;
    private readonly IRealTimeMessaging _realtime;
    private readonly ILogger<LessonService> _logger;

    public LessonService(
        IApplicationDbContext db,
        ITenantContext tenantContext,
        IEmailService email,
        IRealTimeMessaging realtime,
        ILogger<LessonService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _email = email;
        _realtime = realtime;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LessonDto>> CreateAsync(CreateLessonRequest request, CancellationToken ct = default)
    {
        ValidateTimeRange(request.StartTime, request.EndTime);

        var maxStudents = Math.Clamp(request.MaxStudents, 1, 100);
        var studentIds = request.StudentIds?.Distinct().ToList() ?? [];
        if (studentIds.Count > maxStudents)
            throw new InvalidOperationException(
                $"Cette séance est limitée à {maxStudents} élève(s). Vous en avez sélectionné {studentIds.Count}.");

        var tenantId = RequireTenantId();
        var slots = BuildRecurrenceSlots(request.StartTime, request.EndTime, request);
        var created = new List<Lesson>();

        foreach (var (start, end) in slots)
        {
            var lesson = new Lesson
            {
                TenantId = tenantId,
                Title = request.Title.Trim(),
                Description = request.Description?.Trim(),
                Subject = request.Subject?.Trim(),
                StartTime = start,
                EndTime = end,
                Mode = request.Mode,
                Location = request.Location?.Trim(),
                MeetingUrl = request.MeetingUrl?.Trim(),
                SessionNotes = request.SessionNotes?.Trim(),
                MaxStudents = maxStudents
            };
            _db.Add(lesson);
            created.Add(lesson);
        }

        await _db.SaveChangesAsync(ct);

        if (studentIds.Count > 0)
        {
            foreach (var lesson in created)
            {
                foreach (var studentId in studentIds)
                {
                    var student = _db.Students.FirstOrDefault(s => s.Id == studentId);
                    if (student is null) continue;

                    _db.Add(new LessonAttendance
                    {
                        TenantId = tenantId,
                        LessonId = lesson.Id,
                        StudentId = student.Id,
                        IsPresent = false
                    });
                }
            }

            await _db.SaveChangesAsync(ct);
            await SendLessonScheduledEmailsAsync(created[0], studentIds, ct);
        }

        return MapManyToDto(created);
    }

    private static List<(DateTime Start, DateTime End)> BuildRecurrenceSlots(
        DateTime start,
        DateTime end,
        CreateLessonRequest request)
    {
        var duration = end - start;
        var frequency = (request.RecurrenceFrequency ?? "none").Trim().ToLowerInvariant();
        if (frequency is "" or "none" or "once")
            return [(start, end)];

        var step = frequency switch
        {
            "weekly" => TimeSpan.FromDays(7),
            "biweekly" or "fortnightly" => TimeSpan.FromDays(14),
            "monthly" => TimeSpan.Zero, // handled specially
            _ => throw new InvalidOperationException(
                "Récurrence invalide. Utilisez none, weekly, biweekly ou monthly.")
        };

        const int maxOccurrences = 52;
        var until = request.RecurrenceUntil?.Date;
        var targetCount = request.RecurrenceOccurrences is > 1
            ? Math.Min(request.RecurrenceOccurrences.Value, maxOccurrences)
            : maxOccurrences;

        if (until is null && request.RecurrenceOccurrences is null or < 2)
            throw new InvalidOperationException(
                "Pour une récurrence, indiquez le nombre de séances (2–52) ou une date de fin.");

        var slots = new List<(DateTime, DateTime)> { (start, end) };
        var cursor = start;

        while (slots.Count < targetCount)
        {
            cursor = frequency == "monthly"
                ? cursor.AddMonths(1)
                : cursor.Add(step);

            if (until is not null && cursor.Date > until.Value)
                break;

            slots.Add((cursor, cursor + duration));
        }

        return slots;
    }

    private async Task SendLessonScheduledEmailsAsync(Lesson lesson, IReadOnlyList<Guid> studentIds, CancellationToken ct)
    {
        var tutorName = _db.Tenants.FirstOrDefault(t => t.Id == lesson.TenantId)?.Name ?? "Votre tuteur";
        var subject = lesson.Subject ?? lesson.Title;

        foreach (var studentId in studentIds)
        {
            try
            {
                var student = _db.Students.FirstOrDefault(s => s.Id == studentId);
                if (student is null) continue;

                if (!string.IsNullOrWhiteSpace(student.Email))
                    await _email.SendLessonScheduledAsync(student.Email, $"{student.FirstName} {student.LastName}".Trim(), tutorName, subject, lesson.StartTime, ct);

                if (student.IsMinor)
                {
                    var parent = _db.ParentProfiles.FirstOrDefault(p => p.Id == student.ParentProfileId);
                    if (parent is not null && !string.IsNullOrWhiteSpace(parent.Email))
                        await _email.SendLessonScheduledAsync(parent.Email, parent.FirstName, tutorName, subject, lesson.StartTime, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Échec d'envoi d'e-mail de cours planifié pour l'étudiant {StudentId}", studentId);
            }
        }
    }

    private async Task SendLessonCancelledEmailsAsync(Lesson lesson, CancellationToken ct)
    {
        var tutorName = _db.Tenants.FirstOrDefault(t => t.Id == lesson.TenantId)?.Name ?? "Votre tuteur";
        var subject = lesson.Subject ?? lesson.Title;

        var studentIds = _db.LessonAttendances
            .Where(a => a.LessonId == lesson.Id)
            .Select(a => a.StudentId)
            .ToList();

        foreach (var studentId in studentIds)
        {
            try
            {
                var student = _db.Students.FirstOrDefault(s => s.Id == studentId);
                if (student is null) continue;

                if (!string.IsNullOrWhiteSpace(student.Email))
                    await _email.SendLessonCancelledAsync(student.Email, $"{student.FirstName} {student.LastName}".Trim(), tutorName, subject, lesson.StartTime, ct);

                if (student.IsMinor)
                {
                    var parent = _db.ParentProfiles.FirstOrDefault(p => p.Id == student.ParentProfileId);
                    if (parent is not null && !string.IsNullOrWhiteSpace(parent.Email))
                        await _email.SendLessonCancelledAsync(parent.Email, parent.FirstName, tutorName, subject, lesson.StartTime, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Échec d'envoi d'e-mail d'annulation pour l'étudiant {StudentId}", studentId);
            }
        }
    }

    public Task<LessonDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var lesson = _db.Lessons.FirstOrDefault(l => l.Id == id);
        return Task.FromResult(lesson is null ? null : MapToDto(lesson, LoadStudentNames([lesson.Id])));
    }

    public async Task<IReadOnlyList<LessonDto>> GetByDateRangeAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        ValidateDateRange(start, end);
        await SettleDueLessonsAsync(ct);

        var lessons = _db.Lessons
            .Where(l => l.StartTime < end && l.EndTime > start)
            .OrderBy(l => l.StartTime)
            .ToList();

        return MapManyToDto(lessons);
    }

    public async Task<IReadOnlyList<LessonDto>> GetByViewAsync(CalendarView view, DateTime date, CancellationToken ct = default)
    {
        var (start, end) = CalendarRangeHelper.GetViewRange(view, date);
        return await GetByDateRangeAsync(start, end, ct);
    }

    public async Task<LessonDto> UpdateAsync(Guid id, UpdateLessonRequest request, CancellationToken ct = default)
    {
        ValidateTimeRange(request.StartTime, request.EndTime);

        var lesson = _db.Lessons.FirstOrDefault(l => l.Id == id)
            ?? throw new InvalidOperationException("Cours introuvable.");

        if (lesson.SettlementStatus is LessonSettlementStatus.CancelledFree
            or LessonSettlementStatus.TutorNoShow
            or LessonSettlementStatus.LiabilityResolved)
            throw new InvalidOperationException("Ce cours est clôturé et ne peut plus être modifié.");

        var oldStart = ToUtc(lesson.StartTime);
        var oldEnd = ToUtc(lesson.EndTime);
        var newStart = ToUtc(request.StartTime);
        var newEnd = ToUtc(request.EndTime);
        var scheduleChanged = oldStart != newStart || oldEnd != newEnd;

        if (scheduleChanged)
        {
            var now = DateTime.UtcNow;
            if (oldStart - now < TimeSpan.FromHours(CancelFreeHours))
                throw new InvalidOperationException(
                    $"Impossible de modifier la date/heure à moins de {CancelFreeHours} h avant le début du cours.");
        }

        var maxStudents = Math.Clamp(request.MaxStudents, 1, 100);
        var enrolled = _db.LessonAttendances.Count(a => a.LessonId == lesson.Id);
        if (enrolled > maxStudents)
            throw new InvalidOperationException(
                $"Impossible de limiter à {maxStudents} élève(s) : {enrolled} déjà inscrit(s).");

        lesson.Title = request.Title.Trim();
        lesson.Description = request.Description?.Trim();
        lesson.Subject = request.Subject?.Trim();
        lesson.StartTime = request.StartTime;
        lesson.EndTime = request.EndTime;
        lesson.Mode = request.Mode;
        lesson.Location = request.Location?.Trim();
        lesson.MeetingUrl = request.MeetingUrl?.Trim();
        lesson.SessionNotes = request.SessionNotes?.Trim();
        lesson.MaxStudents = maxStudents;
        lesson.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        if (scheduleChanged)
        {
            var studentIds = _db.LessonAttendances
                .Where(a => a.LessonId == lesson.Id)
                .Select(a => a.StudentId)
                .Distinct()
                .ToList();
            if (studentIds.Count > 0)
                await SendLessonScheduledEmailsAsync(lesson, studentIds, ct);
        }

        return MapToDto(lesson, LoadStudentNames([lesson.Id]));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // Soft-cancel selon la règle 24 h (au lieu d'une suppression pure).
        await CancelAsync(id, new CancelLessonRequest("Suppression / annulation"), ct);
    }

    public async Task<LessonDto> CancelAsync(Guid id, CancelLessonRequest request, CancellationToken ct = default)
    {
        var lesson = _db.Lessons.FirstOrDefault(l => l.Id == id)
            ?? throw new InvalidOperationException("Cours introuvable.");

        if (lesson.SettlementStatus is LessonSettlementStatus.CancelledFree
            or LessonSettlementStatus.TutorNoShow
            or LessonSettlementStatus.LiabilityResolved)
            throw new InvalidOperationException("Ce cours est déjà clôturé et ne peut plus être annulé.");

        var now = DateTime.UtcNow;
        var startUtc = ToUtc(lesson.StartTime);
        lesson.CancelledAt = now;
        lesson.CancellationReason = string.IsNullOrWhiteSpace(request.Reason)
            ? null
            : request.Reason.Trim();
        lesson.UpdatedAt = now;

        if (startUtc - now >= TimeSpan.FromHours(CancelFreeHours))
        {
            // Annulation ≥ 24 h : non comptabilisée
            if (lesson.SessionCounted)
                RestoreSessionCredits(lesson);

            lesson.SettlementStatus = LessonSettlementStatus.CancelledFree;
            lesson.SessionCounted = false;
            lesson.TutorLiable = false;
        }
        else
        {
            // Annulation tardive ou après début : validée (comptée)
            ApplyValidation(lesson);
        }

        await SendLessonCancelledEmailsAsync(lesson, ct);
        await _db.SaveChangesAsync(ct);
        return MapToDto(lesson);
    }

    public async Task<LessonDto> MarkTutorNoShowAsync(Guid id, MarkTutorNoShowRequest request, CancellationToken ct = default)
    {
        var lesson = _db.Lessons.FirstOrDefault(l => l.Id == id)
            ?? throw new InvalidOperationException("Cours introuvable.");

        if (lesson.SettlementStatus == LessonSettlementStatus.CancelledFree)
            throw new InvalidOperationException("Cours déjà annulé à temps — rien à comptabiliser.");

        if (lesson.SessionCounted)
            RestoreSessionCredits(lesson);

        lesson.SettlementStatus = LessonSettlementStatus.TutorNoShow;
        lesson.SessionCounted = false;
        lesson.TutorLiable = true;
        lesson.TutorLiabilityResolution = null;
        lesson.TutorLiabilityResolvedAt = null;
        if (!string.IsNullOrWhiteSpace(request.Notes))
            lesson.SessionNotes = string.IsNullOrWhiteSpace(lesson.SessionNotes)
                ? request.Notes.Trim()
                : $"{lesson.SessionNotes}\n[Moniteur absent] {request.Notes.Trim()}";
        lesson.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(lesson);
    }

    public async Task<LessonDto> ResolveTutorLiabilityAsync(
        Guid id,
        ResolveTutorLiabilityRequest request,
        CancellationToken ct = default)
    {
        var lesson = _db.Lessons.FirstOrDefault(l => l.Id == id)
            ?? throw new InvalidOperationException("Cours introuvable.");

        if (!lesson.TutorLiable && lesson.SettlementStatus != LessonSettlementStatus.TutorNoShow)
            throw new InvalidOperationException("Aucune imputation moniteur à résoudre pour ce cours.");

        var resolution = (request.Resolution ?? "").Trim().ToLowerInvariant();
        if (resolution is not ("reschedule" or "refund"))
            throw new InvalidOperationException("Indiquez 'reschedule' (replanifier) ou 'refund' (rembourser).");

        lesson.TutorLiable = false;
        lesson.SettlementStatus = LessonSettlementStatus.LiabilityResolved;
        lesson.TutorLiabilityResolution = resolution;
        lesson.TutorLiabilityResolvedAt = DateTime.UtcNow;
        lesson.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(lesson);
    }

    public Task<IReadOnlyList<LessonAttendanceDto>> GetAttendancesAsync(Guid lessonId, CancellationToken ct = default)
    {
        if (!_db.Lessons.Any(l => l.Id == lessonId))
            throw new InvalidOperationException("Cours introuvable.");

        var rows = _db.LessonAttendances.Where(a => a.LessonId == lessonId).ToList();
        var studentIds = rows.Select(r => r.StudentId).ToList();
        var students = _db.Students.Where(s => studentIds.Contains(s.Id)).ToDictionary(s => s.Id);

        var result = rows.Select(a =>
        {
            students.TryGetValue(a.StudentId, out var student);
            var name = student is null ? "" : $"{student.FirstName} {student.LastName}".Trim();
            return new LessonAttendanceDto(a.StudentId, name, a.IsPresent, a.Notes);
        }).ToList();

        return Task.FromResult<IReadOnlyList<LessonAttendanceDto>>(result);
    }

    public async Task<LessonAttendanceDto> SetAttendanceAsync(
        Guid lessonId,
        SetAttendanceRequest request,
        CancellationToken ct = default)
    {
        var lesson = _db.Lessons.FirstOrDefault(l => l.Id == lessonId)
            ?? throw new InvalidOperationException("Cours introuvable.");

        var attendance = _db.LessonAttendances
            .FirstOrDefault(a => a.LessonId == lessonId && a.StudentId == request.StudentId)
            ?? throw new InvalidOperationException("Élève non inscrit à ce cours.");

        attendance.IsPresent = request.IsPresent;
        attendance.Notes = request.Notes?.Trim();
        attendance.UpdatedAt = DateTime.UtcNow;

        // Élève absent mais cours non annulé à temps → la séance reste / devient validée (comptée).
        if (!request.IsPresent
            && lesson.SettlementStatus == LessonSettlementStatus.Scheduled
            && ToUtc(lesson.EndTime) <= DateTime.UtcNow)
        {
            ApplyValidation(lesson);
        }

        await _db.SaveChangesAsync(ct);

        var student = _db.Students.FirstOrDefault(s => s.Id == request.StudentId);
        var name = student is null ? "" : $"{student.FirstName} {student.LastName}".Trim();
        return new LessonAttendanceDto(attendance.StudentId, name, attendance.IsPresent, attendance.Notes);
    }

    public async Task SettleDueLessonsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var due = _db.Lessons
            .Where(l => l.SettlementStatus == LessonSettlementStatus.Scheduled && l.EndTime <= now)
            .ToList();

        if (due.Count == 0)
            return;

        foreach (var lesson in due)
            ApplyValidation(lesson);

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Cours tenu ou annulé trop tard → validé et séance déduite du forfait.</summary>
    private void ApplyValidation(Lesson lesson)
    {
        if (lesson.SettlementStatus is LessonSettlementStatus.TutorNoShow
            or LessonSettlementStatus.CancelledFree
            or LessonSettlementStatus.LiabilityResolved)
            return;

        lesson.SettlementStatus = LessonSettlementStatus.Validated;
        lesson.TutorLiable = false;

        if (lesson.SessionCounted)
            return;

        var studentIds = _db.LessonAttendances
            .Where(a => a.LessonId == lesson.Id)
            .Select(a => a.StudentId)
            .Distinct()
            .ToList();

        foreach (var studentId in studentIds)
            ConsumeSessionCredit(studentId, lesson.TenantId);

        lesson.SessionCounted = true;
        lesson.UpdatedAt = DateTime.UtcNow;
    }

    private void RestoreSessionCredits(Lesson lesson)
    {
        var studentIds = _db.LessonAttendances
            .Where(a => a.LessonId == lesson.Id)
            .Select(a => a.StudentId)
            .Distinct()
            .ToList();

        foreach (var studentId in studentIds)
        {
            var sub = _db.StudentSubscriptions
                .Where(s => s.StudentId == studentId
                            && s.TenantId == lesson.TenantId
                            && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Paused))
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefault();
            if (sub is null) continue;
            sub.SessionsRemaining += 1;
            sub.UpdatedAt = DateTime.UtcNow;
        }

        lesson.SessionCounted = false;
    }

    private void ConsumeSessionCredit(Guid studentId, Guid tenantId)
    {
        var sub = _db.StudentSubscriptions
            .Where(s => s.StudentId == studentId
                        && s.TenantId == tenantId
                        && s.Status == SubscriptionStatus.Active
                        && s.SessionsRemaining > 0)
            .OrderBy(s => s.EndDate)
            .FirstOrDefault();
        if (sub is null) return;
        sub.SessionsRemaining -= 1;
        sub.UpdatedAt = DateTime.UtcNow;
    }

    public async Task NotifySessionStartedAsync(Guid lessonId, CancellationToken ct = default)
    {
        var lesson = _db.Lessons.FirstOrDefault(l => l.Id == lessonId)
            ?? throw new InvalidOperationException("Cours introuvable.");

        if (lesson.SettlementStatus is LessonSettlementStatus.CancelledFree
            or LessonSettlementStatus.TutorNoShow
            or LessonSettlementStatus.LiabilityResolved)
            throw new InvalidOperationException("Ce cours est clôturé — impossible de démarrer la salle.");

        var studentIds = _db.LessonAttendances
            .Where(a => a.LessonId == lessonId)
            .Select(a => a.StudentId)
            .Distinct()
            .ToList();

        var students = _db.Students.Where(s => studentIds.Contains(s.Id)).ToList();
        var recipientIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var student in students)
        {
            if (!string.IsNullOrWhiteSpace(student.UserId))
                recipientIds.Add(student.UserId);

            if (student.IsMinor && student.ParentProfileId is Guid parentId)
            {
                var parent = _db.ParentProfiles.FirstOrDefault(p => p.Id == parentId);
                if (parent is not null && !string.IsNullOrWhiteSpace(parent.UserId))
                    recipientIds.Add(parent.UserId);
            }
        }

        var tutorName = _db.Tenants.FirstOrDefault(t => t.Id == lesson.TenantId)?.Name ?? "Votre tuteur";
        var payload = new LessonStartedNotificationDto(
            lesson.Id,
            lesson.Title,
            lesson.Subject,
            tutorName,
            DateTime.UtcNow);

        await _realtime.NotifyLessonStartedAsync(recipientIds, payload, ct);
        _logger.LogInformation(
            "Notification démarrage séance {LessonId} envoyée à {Count} destinataire(s).",
            lessonId,
            recipientIds.Count);
    }

    private static DateTime ToUtc(DateTime dt) =>
        dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };

    private Guid RequireTenantId()
    {
        if (!_tenantContext.HasTenant || _tenantContext.TenantId is null)
            throw new InvalidOperationException("Contexte locataire requis.");

        return _tenantContext.TenantId.Value;
    }

    private static void ValidateTimeRange(DateTime start, DateTime end)
    {
        if (end <= start)
            throw new InvalidOperationException("L'heure de fin doit être postérieure à l'heure de début.");
    }

    private static void ValidateDateRange(DateTime start, DateTime end)
    {
        if (end <= start)
            throw new InvalidOperationException("La date de fin doit être postérieure à la date de début.");
    }

    private IReadOnlyList<LessonDto> MapManyToDto(IReadOnlyList<Lesson> lessons)
    {
        var names = LoadStudentNames(lessons.Select(l => l.Id));
        return lessons.Select(l => MapToDto(l, names)).ToList();
    }

    private Dictionary<Guid, List<string>> LoadStudentNames(IEnumerable<Guid> lessonIds)
    {
        var ids = lessonIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, List<string>>();

        var rows = _db.LessonAttendances.Where(a => ids.Contains(a.LessonId)).ToList();
        var studentIds = rows.Select(r => r.StudentId).Distinct().ToList();
        var students = _db.Students.Where(s => studentIds.Contains(s.Id)).ToDictionary(s => s.Id);

        return rows
            .GroupBy(r => r.LessonId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a =>
                {
                    students.TryGetValue(a.StudentId, out var student);
                    return student is null ? "" : $"{student.FirstName} {student.LastName}".Trim();
                }).Where(n => n.Length > 0).ToList());
    }

    private static LessonDto MapToDto(
        Lesson lesson,
        Dictionary<Guid, List<string>>? studentNamesByLesson = null)
    {
        IReadOnlyList<string>? names = null;
        if (studentNamesByLesson is not null && studentNamesByLesson.TryGetValue(lesson.Id, out var list))
            names = list;

        return new(
            lesson.Id,
            lesson.Title,
            lesson.Description,
            lesson.Subject,
            lesson.StartTime,
            lesson.EndTime,
            lesson.Mode.ToString(),
            lesson.Location,
            lesson.MeetingUrl,
            lesson.SessionNotes,
            lesson.CreatedAt,
            lesson.UpdatedAt,
            lesson.SettlementStatus.ToString(),
            lesson.CancelledAt,
            lesson.SessionCounted,
            lesson.TutorLiable,
            lesson.TutorLiabilityResolution,
            lesson.MaxStudents,
            names);
    }
}
