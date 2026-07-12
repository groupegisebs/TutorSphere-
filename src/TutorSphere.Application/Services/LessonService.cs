using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Calendar;
using TutorSphere.Application.DTOs.Lessons;
using TutorSphere.Domain.Entities;
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
}

public class LessonService : ILessonService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IEmailService _email;
    private readonly ILogger<LessonService> _logger;

    public LessonService(IApplicationDbContext db, ITenantContext tenantContext, IEmailService email, ILogger<LessonService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _email = email;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LessonDto>> CreateAsync(CreateLessonRequest request, CancellationToken ct = default)
    {
        ValidateTimeRange(request.StartTime, request.EndTime);

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
                SessionNotes = request.SessionNotes?.Trim()
            };
            _db.Add(lesson);
            created.Add(lesson);
        }

        await _db.SaveChangesAsync(ct);

        if (request.StudentIds is { Count: > 0 })
        {
            foreach (var lesson in created)
            {
                foreach (var studentId in request.StudentIds.Distinct())
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
            await SendLessonScheduledEmailsAsync(created[0], request.StudentIds, ct);
        }

        return created.Select(MapToDto).ToList();
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
        return Task.FromResult(lesson is null ? null : MapToDto(lesson));
    }

    public Task<IReadOnlyList<LessonDto>> GetByDateRangeAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        ValidateDateRange(start, end);

        var lessons = _db.Lessons
            .Where(l => l.StartTime < end && l.EndTime > start)
            .OrderBy(l => l.StartTime)
            .ToList()
            .Select(MapToDto)
            .ToList();

        return Task.FromResult<IReadOnlyList<LessonDto>>(lessons);
    }

    public Task<IReadOnlyList<LessonDto>> GetByViewAsync(CalendarView view, DateTime date, CancellationToken ct = default)
    {
        var (start, end) = CalendarRangeHelper.GetViewRange(view, date);
        return GetByDateRangeAsync(start, end, ct);
    }

    public async Task<LessonDto> UpdateAsync(Guid id, UpdateLessonRequest request, CancellationToken ct = default)
    {
        ValidateTimeRange(request.StartTime, request.EndTime);

        var lesson = _db.Lessons.FirstOrDefault(l => l.Id == id)
            ?? throw new InvalidOperationException("Cours introuvable.");

        lesson.Title = request.Title.Trim();
        lesson.Description = request.Description?.Trim();
        lesson.Subject = request.Subject?.Trim();
        lesson.StartTime = request.StartTime;
        lesson.EndTime = request.EndTime;
        lesson.Mode = request.Mode;
        lesson.Location = request.Location?.Trim();
        lesson.MeetingUrl = request.MeetingUrl?.Trim();
        lesson.SessionNotes = request.SessionNotes?.Trim();
        lesson.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return MapToDto(lesson);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var lesson = _db.Lessons.FirstOrDefault(l => l.Id == id)
            ?? throw new InvalidOperationException("Cours introuvable.");

        await SendLessonCancelledEmailsAsync(lesson, ct);

        foreach (var attendance in _db.LessonAttendances.Where(a => a.LessonId == id).ToList())
            _db.Remove(attendance);

        foreach (var report in _db.LessonReports.Where(r => r.LessonId == id).ToList())
            _db.Remove(report);

        foreach (var homework in _db.Homeworks.Where(h => h.LessonId == id).ToList())
            homework.LessonId = null;

        foreach (var document in _db.Documents.Where(d => d.LessonId == id).ToList())
            document.LessonId = null;

        _db.Remove(lesson);
        await _db.SaveChangesAsync(ct);
    }

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

    private static LessonDto MapToDto(Lesson lesson) => new(
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
        lesson.UpdatedAt);
}
