using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Calendar;
using TutorSphere.Application.DTOs.Lessons;
using TutorSphere.Domain.Entities;

namespace TutorSphere.Application.Services;

public interface ILessonService
{
    Task<LessonDto> CreateAsync(CreateLessonRequest request, CancellationToken ct = default);
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

    public LessonService(IApplicationDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<LessonDto> CreateAsync(CreateLessonRequest request, CancellationToken ct = default)
    {
        ValidateTimeRange(request.StartTime, request.EndTime);

        var lesson = new Lesson
        {
            TenantId = RequireTenantId(),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Subject = request.Subject?.Trim(),
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Mode = request.Mode,
            Location = request.Location?.Trim(),
            MeetingUrl = request.MeetingUrl?.Trim(),
            SessionNotes = request.SessionNotes?.Trim()
        };

        _db.Add(lesson);
        await _db.SaveChangesAsync(ct);

        return MapToDto(lesson);
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
